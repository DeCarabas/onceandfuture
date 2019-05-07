namespace OnceAndFuture.Syndication
{
    using OnceAndFuture.DAL;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;

    public class RiverLoader
    {
        /// <summary>The number of updates to have in a river before archiving.</summary>
        const int UpdateLimit = 40;

        /// <summary>The number of updates to send to the archive.</summary>
        const int UpdateSize = 20;

        static readonly HttpClient client = Policies.CreateHttpClient(allowRedirect: false);

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

            var parser = new RiverLoader();
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
                    response = await Policies.HttpPolicy.ExecuteAsync(_ =>
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
                    result = FeedParser.ParseFeed(responseUri, element, out FeedFormat format);
                    if (result != null)
                    {
                        Log.EndGetFeed(uri, "ok", format.ToString(), response, result, loadTimer);
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
