namespace OnceAndFuture.Syndication
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;

    public class RiverFeedParser
    {
        /// <summary>The number of updates to have in a river before archiving.</summary>
        const int UpdateLimit = 40;

        /// <summary>The number of updates to send to the archive.</summary>
        const int UpdateSize = 20;

        static readonly HttpClient client = Policies.CreateHttpClient(allowRedirect: false);

        static readonly Dictionary<XName, Func<FeedSegment, XElement, FeedSegment>> FeedElements =
            new Dictionary<XName, Func<FeedSegment, XElement, FeedSegment>>
            {
                { XNames.RSS.Title,       (rf, xe) => rf.With(feedTitle: SyndicationUtil.ParseBody(xe)) },
                { XNames.RSS10.Title,     (rf, xe) => rf.With(feedTitle: SyndicationUtil.ParseBody(xe)) },
                { XNames.Atom.Title,      (rf, xe) => rf.With(feedTitle: SyndicationUtil.ParseBody(xe)) },

                { XNames.RSS.Link,        (rf, xe) => rf.With(websiteUrl: xe.Value) },
                { XNames.RSS10.Link,      (rf, xe) => rf.With(websiteUrl: xe.Value) },
                { XNames.Atom.Link,       (rf, xe) => HandleAtomLink(rf, xe) },

                { XNames.RSS.Description,   (rf, xe) => rf.With(feedDescription: SyndicationUtil.ParseBody(xe)) },
                { XNames.RSS10.Description, (rf, xe) => rf.With(feedDescription: SyndicationUtil.ParseBody(xe)) },

                { XNames.RSS.Item,        (rf, xe) => rf.With(items: rf.Items.Add(LoadItem(xe))) },
                { XNames.RSS10.Item,      (rf, xe) => rf.With(items: rf.Items.Add(LoadItem(xe))) },
                { XNames.Atom.Entry,      (rf, xe) => rf.With(items: rf.Items.Add(LoadItem(xe))) },
            };

        static readonly Dictionary<XName, Func<Item, XElement, Item>> ItemElements =
            new Dictionary<XName, Func<Item, XElement, Item>>
            {
                { XNames.RSS.Title,       (ri, xe) => ri.With(title: SyndicationUtil.ParseBody(xe)) },
                { XNames.RSS.Link,        (ri, xe) => ri.With(link: SyndicationUtil.ParseLink(xe.Value, xe)) },
                { XNames.RSS.Description, (ri, xe) => ri.With(description: xe) },
                { XNames.RSS.Comments,    (ri, xe) => ri.With(comments: xe.Value) },
                { XNames.RSS.PubDate,     (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.RSS.Guid,        (ri, xe) => HandleGuid(ri, xe) },
                { XNames.RSS.Enclosure,   (ri, xe) => HandleEnclosure(ri, xe) },

                { XNames.RSS10.Title,       (ri, xe) => ri.With(title: SyndicationUtil.ParseBody(xe)) },
                { XNames.RSS10.Link,        (ri, xe) => ri.With(link: SyndicationUtil.ParseLink(xe.Value, xe)) },
                { XNames.RSS10.Description, (ri, xe) => ri.With(description: xe) },
                { XNames.RSS10.Comments,    (ri, xe) => ri.With(comments: xe.Value) },
                { XNames.RSS10.PubDate,     (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.RSS10.Guid,        (ri, xe) => HandleGuid(ri, xe) },

                { XNames.Content.Encoded,  (ri, xe) => ri.With(content: xe) },

                { XNames.Atom.Title,       (ri, xe) => ri.With(title: SyndicationUtil.ParseBody(xe)) },
                { XNames.Atom.Content,     (ri, xe) => ri.With(content: xe) },
                { XNames.Atom.Summary,     (ri, xe) => ri.With(summary: xe) },
                { XNames.Atom.Link,        (ri, xe) => HandleAtomLink(ri, xe) },
                { XNames.Atom.Id,          (ri, xe) => ri.With(id: xe.Value) },
                { XNames.Atom.Published,   (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.Atom.Updated,     (ri, xe) => HandlePubDate(ri, xe) },

                { XNames.Media.Content, (ri, xe) => HandleThumbnail(ri, xe) },
        };

        readonly AggregateRiverStore aggregateStore = new AggregateRiverStore();
        readonly RiverArchiveStore archiveStore = new RiverArchiveStore();
        readonly RiverFeedStore feedStore = new RiverFeedStore();
        readonly ThumbnailExtractor thumbnailExtractor = new ThumbnailExtractor();

        public async Task<River> FetchAndUpdateRiver(Uri uri)
        {
            River river = await feedStore.LoadRiverForFeed(uri);
            if ((river.Metadata.LastStatus != HttpStatusCode.MovedPermanently) &&
                (river.Metadata.LastStatus != HttpStatusCode.Gone))
            {
                river = await UpdateAsync(river);
                await WriteRiver(uri, river);
            }

            if (river.Metadata.LastStatus == HttpStatusCode.MovedPermanently)
            {
                return await FetchAndUpdateRiver(river.Metadata.OriginUrl);
            }

            return river;
        }

        public async Task<River> FetchRiver(Uri uri)
        {
            River river = await feedStore.LoadRiverForFeed(uri);
            while (river.Metadata.LastStatus == HttpStatusCode.MovedPermanently)
            {
                river = await feedStore.LoadRiverForFeed(river.Metadata.OriginUrl);
            }
            return river;
        }

        async Task<River> WriteRiver(Uri uri, River river)
        {
            river = await MaybeArchiveRiver(uri.AbsoluteUri, river);
            await feedStore.WriteRiver(uri, river);
            return river;
        }

        async Task<River> MaybeArchiveRiver(string id, River river)
        {
            if (river.UpdatedFeeds.Feeds.Count > UpdateLimit)
            {
                Log.SplittingFeed(id, river);
                ImmutableList<FeedSegment> feeds = river.UpdatedFeeds.Feeds;
                IEnumerable<FeedSegment> oldFeeds = feeds.Skip(UpdateSize);
                IEnumerable<FeedSegment> newFeeds = feeds.Take(UpdateSize);

                River oldRiver = river.With(updatedFeeds: river.UpdatedFeeds.With(feeds: oldFeeds));
                string archiveKey = await this.archiveStore.WriteRiverArchive(oldRiver);
                Log.WroteArchive(id, river, archiveKey);

                river = river.With(
                    updatedFeeds: river.UpdatedFeeds.With(feeds: newFeeds),
                    metadata: river.Metadata.With(next: archiveKey)
                );
            }

            return river;
        }

        public async Task<River> RefreshAggregateRiverWithFeeds(string id, IList<Uri> feedUrls)
        {
            Stopwatch aggregateTimer = Stopwatch.StartNew();

            River river = await this.aggregateStore.LoadAggregate(id);

            Log.AggregateRefreshStart(id, feedUrls.Count);
            DateTimeOffset lastUpdated = river.UpdatedFeeds.Feeds.Count > 0
                ? river.UpdatedFeeds.Feeds.Max(f => f.WhenLastUpdate)
                : DateTimeOffset.MinValue;

            var parser = new RiverFeedParser();
            River[] rivers = await Task.WhenAll(from url in feedUrls select parser.FetchRiver(url));
            Log.AggregateRefreshPulledRivers(id, rivers.Length);

            var newFeeds = new List<FeedSegment>();
            foreach (River feedRiver in rivers)
            {
                FeedSegment[] newUpdates =
                    feedRiver.UpdatedFeeds.Feeds.Where(rf => rf.WhenLastUpdate > lastUpdated).ToArray();
                Log.AggregateNewUpdates(id, feedRiver.Metadata.OriginUrl, newUpdates.Length);

                if (newUpdates.Length > 0)
                {
                    Item[] newItems = newUpdates.SelectMany(rf => rf.Items).ToArray();
                    DateTimeOffset biggestUpdate = newUpdates.Max(rf => rf.WhenLastUpdate);
                    Log.AggregateFeedState(id, feedRiver.Metadata.OriginUrl, newUpdates.Length, biggestUpdate);
                    newFeeds.Add(newUpdates[0].With(whenLastUpdate: biggestUpdate, items: newItems));
                }
            }

            // Sort all the new feeds by time they were updated (latest time first).
            Log.AggregateHasNewFeeds(id, newFeeds.Count);
            newFeeds = newFeeds.OrderByDescending(f => f.WhenLastUpdate).ToList();
            River newRiver = river.With(
                updatedFeeds: river.UpdatedFeeds.With(feeds: newFeeds.Concat(river.UpdatedFeeds.Feeds)));

            newRiver = await MaybeArchiveRiver(id, newRiver);

            Log.UpdatingAggregate(id);
            await this.aggregateStore.WriteAggregate(id, newRiver);

            Log.AggregateRefreshed(id, aggregateTimer);
            return newRiver;
        }

        public async Task<River> UpdateAsync(River river)
        {
            FetchResult fetchResult = await FetchAsync(
                river.Metadata.OriginUrl,
                river.Metadata.Etag,
                river.Metadata.LastModified
            );

            var updatedFeeds = river.UpdatedFeeds;
            if (fetchResult.Feed != null)
            {
                var feed = fetchResult.Feed;
                var existingItems = new HashSet<string>(
                    from existingFeed in river.UpdatedFeeds.Feeds
                    from item in existingFeed.Items
                    where item.Id != null
                    select item.Id
                );
                Item[] newItems = feed.Items.Where(item => !existingItems.Contains(item.Id)).ToArray();
                if (newItems.Length > 0)
                {
                    Uri baseUri = SyndicationUtil.TryParseAbsoluteUrl(feed.WebsiteUrl) ?? feed.FeedUrl;
                    for (int i = 0; i < newItems.Length; i++)
                    {
                        newItems[i] = Rebase(newItems[i], baseUri);
                    }

                    newItems = await this.thumbnailExtractor.LoadItemThumbnailsAsync(baseUri, newItems);
                    feed = feed.With(items: newItems);
                    updatedFeeds = river.UpdatedFeeds.With(feeds: river.UpdatedFeeds.Feeds.Insert(0, feed));
                }
            }

            var metadata = river.Metadata.With(
                etag: fetchResult.Etag,
                lastModified: fetchResult.LastModified,
                originUrl: fetchResult.FeedUrl,
                lastStatus: fetchResult.Status);

            return river.With(updatedFeeds: updatedFeeds, metadata: metadata);
        }

        static Item Rebase(Item item, Uri baseUri)
        {
            return item.With(
                link: SyndicationUtil.Rebase(item.Link, baseUri),
                permaLink: SyndicationUtil.Rebase(item.PermaLink, baseUri),
                enclosures: item.Enclosures.Select(e => e.With(url: SyndicationUtil.Rebase(e.Url, baseUri)))
            );
        }

        static async Task<FetchResult> FetchAsync(Uri uri, string etag, DateTimeOffset? lastModified)
        {
            Stopwatch loadTimer = Stopwatch.StartNew();
            try
            {
                Log.BeginGetFeed(uri);
                HttpResponseMessage response = null;

                Uri requestUri = uri;
                for (int i = 0; i < 30; i++)
                {
                    response = await Policies.HttpPolicy.ExecuteAsync(() =>
                        {
                            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                            if (etag != null) { request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag)); }
                            request.Headers.IfModifiedSince = lastModified;

                            return client.SendAsync(request);
                        },
                        new Dictionary<string, object> { { "uri", requestUri } });

                    if ((response.StatusCode != HttpStatusCode.TemporaryRedirect) &&
                        (response.StatusCode != HttpStatusCode.Found) &&
                        (response.StatusCode != HttpStatusCode.SeeOther))
                    {
                        break;
                    }

                    Uri newUri = response.Headers.Location;
                    if (!newUri.IsAbsoluteUri) { newUri = new Uri(requestUri, newUri); }
                    requestUri = newUri;
                }

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    Log.EndGetFeedNotModified(uri, response, loadTimer);
                    return new FetchResult(
                        feed: null,
                        status: HttpStatusCode.NotModified,
                        feedUrl: uri,
                        etag: etag,
                        lastModified: lastModified);
                }

                if (response.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    Log.EndGetFeedMovedPermanently(uri, response, loadTimer);

                    Uri newUri = response.Headers.Location;
                    if (!newUri.IsAbsoluteUri) { newUri = new Uri(requestUri, newUri); }

                    return new FetchResult(
                        feed: null,
                        status: HttpStatusCode.MovedPermanently,
                        feedUrl: newUri,
                        etag: etag,
                        lastModified: lastModified);
                }

                if (!response.IsSuccessStatusCode)
                {
                    //string body = await response.Content.ReadAsStringAsync();
                    string body = string.Empty;
                    Log.EndGetFeedFailure(uri, response, body, loadTimer);
                    return new FetchResult(
                        feed: null,
                        status: response.StatusCode,
                        feedUrl: uri,
                        etag: etag,
                        lastModified: lastModified);
                }

                Uri responseUri = response.RequestMessage.RequestUri;

                await response.Content.LoadIntoBufferAsync();
                using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                using (var textReader = new StreamReader(responseStream))
                using (var reader = XmlReader.Create(textReader)) // TODO: BASE URI?
                {
                    FeedSegment result = null;
                    XElement element = XElement.Load(reader, LoadOptions.SetBaseUri);
                    if (element.Name == XNames.RSS.Rss)
                    {
                        result = LoadFeed(responseUri, element.Element(XNames.RSS.Channel));
                        Log.EndGetFeed(uri, "ok", "rss2.0", response, result, loadTimer);
                    }
                    else if (element.Name == XNames.Atom.Feed)
                    {
                        result = LoadFeed(responseUri, element);
                        Log.EndGetFeed(uri, "ok", "atom", response, result, loadTimer);
                    }
                    else if (element.Name == XNames.RDF.Rdf)
                    {
                        result = LoadFeed(responseUri, element.Element(XNames.RSS10.Channel));
                        result = result.With(
                            items: result.Items.AddRange(
                                element.Elements(XNames.RSS10.Item).Select(xe => LoadItem(xe))
                            )
                        );
                        Log.EndGetFeed(uri, "ok", "rdf", response, result, loadTimer);
                    }
                    else
                    {
                        Log.UnrecognizableFeed(uri, response, element.ToString(), loadTimer);
                    }

                    string newEtag = response.Headers.ETag?.Tag;
                    DateTimeOffset? newLastModified = response.Content.Headers.LastModified;

                    return new FetchResult(
                        feed: result,
                        status: HttpStatusCode.OK,
                        feedUrl: responseUri,
                        etag: newEtag,
                        lastModified: newLastModified);
                }
            }
            catch (TaskCanceledException requestException)
            {
                Log.FeedTimeout(uri, loadTimer, requestException);
                return new FetchResult(
                    feed: null,
                    status: 0,
                    feedUrl: uri,
                    etag: etag,
                    lastModified: lastModified);
            }
            catch (HttpRequestException requestException)
            {
                Log.NetworkError(uri, requestException, loadTimer);
                return new FetchResult(
                    feed: null,
                    status: 0,
                    feedUrl: uri,
                    etag: etag,
                    lastModified: lastModified);
            }
            catch (WebException requestException)
            {
                Log.NetworkError(uri, requestException, loadTimer);
                return new FetchResult(
                    feed: null,
                    status: 0,
                    feedUrl: uri,
                    etag: etag,
                    lastModified: lastModified);
            }
            catch (XmlException xmlException)
            {
                Log.XmlError(uri, xmlException, loadTimer);
                return new FetchResult(
                    feed: null,
                    status: 0,
                    feedUrl: uri,
                    etag: etag,
                    lastModified: lastModified);
            }
        }

        static FeedSegment HandleAtomLink(FeedSegment feed, XElement link)
        {
            string rel = link.Attribute(XNames.Atom.Rel)?.Value ?? "alternate";
            string type = link.Attribute(XNames.Atom.Type)?.Value ?? "text/html";
            string href = link.Attribute(XNames.Atom.Href)?.Value;

            if (String.Equals(rel, "alternate", StringComparison.OrdinalIgnoreCase) &&
                type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                feed = feed.With(websiteUrl: link.Attribute(XNames.Atom.Href)?.Value);
            }

            return feed;
        }

        static Item HandleAtomLink(Item item, XElement link)
        {
            string rel = link.Attribute(XNames.Atom.Rel)?.Value ?? "alternate";
            string type = link.Attribute(XNames.Atom.Type)?.Value ?? "text/html";
            string href = link.Attribute(XNames.Atom.Href)?.Value;

            if (String.Equals(rel, "alternate", StringComparison.OrdinalIgnoreCase) &&
                type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                item = item.With(link: SyndicationUtil.ParseLink(href, link));
            }

            if (String.Equals(rel, "self", StringComparison.OrdinalIgnoreCase) &&
                type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                item = item.With(permaLink: SyndicationUtil.ParseLink(href, link));
            }

            if (link.Attribute(XNames.Atom.Rel)?.Value == "enclosure")
            {
                item = item.With(enclosures: item.Enclosures.Add(new Enclosure(
                    length: link.Attribute(XNames.Atom.Length)?.Value,
                    type: type,
                    url: SyndicationUtil.ParseLink(href, link)
                )));
            }
            return item;
        }

        static Item HandleEnclosure(Item item, XElement element)
        {
            return item.With(enclosures: item.Enclosures.Add(new Enclosure(
                length: element.Attribute(XNames.RSS.Length)?.Value,
                type: element.Attribute(XNames.RSS.Type)?.Value,
                url: SyndicationUtil.ParseLink(element.Attribute(XNames.RSS.Url)?.Value, element)
            )));
        }

        static Item HandleGuid(Item item, XElement element)
        {
            item = item.With(id: element.Value);

            if (item.Id.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                item.Id.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                item = item.With(permaLink: SyndicationUtil.ParseLink(item.Id, element));
            }
            return item;
        }

        static Item HandlePubDate(Item item, XElement element)
        {
            DateTime? date = SyndicationUtil.ParseDate(element);
            if (date != null && (item.PubDate == null || date > item.PubDate))
            {
                return item.With(pubDate: date);
            }
            return item;
        }

        static Item HandleThumbnail(Item item, XElement element)
        {
            if (element.Name == XNames.Media.Content && element.Attribute(XNames.Media.Medium)?.Value == "image")
            {
                Uri url = SyndicationUtil.TryParseUrl(element.Attribute(XNames.Media.Url)?.Value, null, element);

                int width, height;
                if (url != null &&
                    Int32.TryParse(element.Attribute(XNames.Media.Width)?.Value, out width) &&
                    Int32.TryParse(element.Attribute(XNames.Media.Height)?.Value, out height))
                {
                    item = item.With(thumbnail: new Thumbnail(url, width, height));
                }
            }

            return item;
        }

        static FeedSegment LoadFeed(Uri feedUrl, XElement item)
        {
            var rf = new FeedSegment(feedUrl: feedUrl);
            foreach (XElement xe in item.Elements())
            {
                Func<FeedSegment, XElement, FeedSegment> action;
                if (FeedElements.TryGetValue(xe.Name, out action)) { rf = action(rf, xe); }
            }
            if (String.IsNullOrWhiteSpace(rf.FeedTitle))
            {
                string title = null;
                if (!String.IsNullOrWhiteSpace(rf.FeedDescription)) { title = rf.FeedDescription; }
                else if (!String.IsNullOrWhiteSpace(rf.WebsiteUrl)) { title = rf.WebsiteUrl; }
                else if (rf.FeedUrl != null) { title = rf.FeedUrl.AbsoluteUri; }

                rf = rf.With(feedTitle: title);
            }
            return rf;
        }

        static Item LoadItem(XElement item)
        {
            var ri = new Item();
            foreach (XElement xe in item.Elements())
            {
                Func<Item, XElement, Item> func;
                if (ItemElements.TryGetValue(xe.Name, out func)) { ri = func(ri, xe); }
            }

            // Load the body; prefer explicit summaries to "description", which is ambiguous, to "content", which is
            // explicitly intended to be the full entry content.
            if (ri.Summary != null) { ri = ri.With(body: SyndicationUtil.ParseBody(ri.Summary)); }
            else if (ri.Description != null) { ri = ri.With(body: SyndicationUtil.ParseBody(ri.Description)); }
            else if (ri.Content != null) { ri = ri.With(body: SyndicationUtil.ParseBody(ri.Content)); }

            if (ri.PermaLink == null) { ri = ri.With(permaLink: ri.Link); }
            if (ri.Id == null) { ri = ri.With(id: CreateItemId(ri)); }
            if (String.IsNullOrWhiteSpace(ri.Title))
            {
                string title = null;
                if (ri.PubDate != null) { title = ri.PubDate.ToString(); }
                else if (ri.PermaLink != null) { title = ri.PermaLink.AbsoluteUri; }
                else if (ri.Id != null) { title = ri.Id; }

                if (title != null) { ri = ri.With(title: title); }
            }
            return ri;
        }

        static string CreateItemId(Item item)
        {
            var guid = "";
            if (item.PubDate != null) { guid += item.PubDate.ToString(); }
            if (item.Link != null) { guid += item.Link; }
            if (item.Title != null) { guid += item.Title; }

            if (guid.Length > 0)
            {
                byte[] hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(guid));
                guid = Convert.ToBase64String(hash);
            }
            return guid;
        }

        class FetchResult
        {
            public FetchResult(
                FeedSegment feed,
                HttpStatusCode status,
                Uri feedUrl,
                string etag,
                DateTimeOffset? lastModified)
            {
                Feed = feed;
                Status = status;
                FeedUrl = feedUrl;
                Etag = etag;
                LastModified = lastModified;
            }

            public FeedSegment Feed { get; }
            public HttpStatusCode Status { get; }
            public Uri FeedUrl { get; }
            public string Etag { get; }
            public DateTimeOffset? LastModified { get; }
        }
    }
}
