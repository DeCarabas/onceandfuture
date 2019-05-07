namespace OnceAndFuture.Syndication
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Html.Parser;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    public static class FeedDetector
    {
        static HttpClient client = Policies.CreateHttpClient();
        static
        HashSet<string> FeedMimeTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/rss+xml",
                "text/xml",
                "application/atom+xml",
                "application/x.atom+xml",
                "application/x-atom+xml",
            };
        static readonly
        string[] OrderedFeedKeywords =
            new string[] {"atom", "rss", "rdf", "xml", "feed"};
        static readonly
        string[] FeedNames =
            new string[]
            {
                "atom.xml",
                "index.atom",
                "index.rdf",
                "rss.xml",
                "index.xml",
                "index.rss",

            };
        static readonly
        string[] FeedExtensions =
            new string[] {".rss", ".rdf", ".xml", ".atom", };

        public static async Task<IList<Uri>> GetFeedUrls(
            string originUrl,
            bool findAll = false)
        {
            var allUrls = new List<Uri>();
            Uri baseUri = FixupUrl(originUrl);

            // Maybe... maybe this one is a feed?
            Log.FindFeedCheckingBase(baseUri);
            string data = await GetFeedData(baseUri);
            if (LooksLikeFeed(data))
            {
                Log.FindFeedBaseWasFeed(baseUri);
                return new[] {baseUri};
            }

            // Nope, let's dive into the soup!
            var parser = new HtmlParser();
            IHtmlDocument document = parser.ParseDocument(data);

            // Link elements.
            Log.FindFeedCheckingLinkElements(baseUri);
            List<Uri> linkUrls = new List<Uri>();
            foreach (IElement element in document.GetElementsByTagName("link"))
            {
                string linkType = element.GetAttribute("type");
                if (linkType != null && FeedMimeTypes.Contains(linkType))
                {
                    Uri hrefUrl =
                        SyndicationUtil.TryParseAbsoluteUrl(
                            element.GetAttribute("href"),
                            baseUri
                        );
                    if (hrefUrl != null)
                    {
                        linkUrls.Add(hrefUrl);
                    }
                }
            }

            await FilterUrlsByFeed(linkUrls);
            if (linkUrls.Count > 0)
            {
                Log.FindFeedFoundLinkElements(baseUri, linkUrls);
                linkUrls.Sort(UrlFeedComparison);
                allUrls.AddRange(linkUrls);
                if (!findAll)
                {
                    return allUrls;
                }
            }

            // <a> tags
            Log.FindFeedCheckingAnchorElements(baseUri);
            List<Uri> localGuesses = new List<Uri>();
            List<Uri> remoteGuesses = new List<Uri>();
            foreach (IElement element in document.GetElementsByTagName("a"))
            {
                Uri hrefUrl =
                    SyndicationUtil.TryParseAbsoluteUrl(
                        element.GetAttribute("href"),
                        baseUri
                    );
                if (hrefUrl != null)
                {
                    if ((hrefUrl.Host == baseUri.Host) && IsFeedUrl(hrefUrl))
                    {
                        localGuesses.Add(hrefUrl);
                    }
                    else if (IsFeedishUrl(hrefUrl))
                    {
                        remoteGuesses.Add(hrefUrl);
                    }
                }
            }

            Log.FindFeedFoundSomeAnchors(baseUri, localGuesses, remoteGuesses);

            // (Consider ones on the same domain first.)
            await FilterUrlsByFeed(localGuesses);
            if (localGuesses.Count > 0)
            {
                Log.FindFeedsFoundLocalGuesses(baseUri, localGuesses);
                localGuesses.Sort(UrlFeedComparison);
                allUrls.AddRange(localGuesses);
                if (!findAll)
                {
                    return localGuesses;
                }
            }

            await FilterUrlsByFeed(remoteGuesses);
            if (remoteGuesses.Count > 0)
            {
                Log.FindFeedsFoundRemoteGuesses(baseUri, remoteGuesses);
                remoteGuesses.Sort(UrlFeedComparison);
                allUrls.AddRange(remoteGuesses);
                if (!findAll)
                {
                    return remoteGuesses;
                }
            }

            List<Uri> randomGuesses =
                FeedNames.Select(s => new Uri(baseUri, s)).ToList();
            await FilterUrlsByFeed(randomGuesses);
            if (randomGuesses.Count > 0)
            {
                Log.FindFeedsFoundRandomGuesses(baseUri, randomGuesses);
                randomGuesses.Sort(UrlFeedComparison);
                allUrls.AddRange(randomGuesses);
                if (!findAll)
                {
                    return randomGuesses;
                }
            }

            // All done, nothing. (Or... everything!)
            Log.FindFeedFoundTotal(baseUri, allUrls);
            return allUrls;
        }

        static Uri FixupUrl(string uri)
        {
            uri = uri.Trim();
            if (uri.StartsWith("feed://"))
            {
                uri = "http://" + uri.Substring(7);
            }
            else if (!(uri.StartsWith("http://") || uri.StartsWith("https://")))
            {
                uri = "http://" + uri;
            }

            Uri parsedUri;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out parsedUri))
            {
                throw new FindFeedException(
                    "The provided URL ({0}) does not seem like a valid URL.",
                    uri
                );
            }

            return parsedUri;
        }

        static async Task<string> GetFeedData(Uri url)
        {
            HttpResponseMessage response =
                await
                    Policies.HttpPolicy.ExecuteAsync(
                        _ => client.GetAsync(url),
                        new Dictionary<string, object> {{"uri", url}}
                    );
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    Log.DetectFeedServerError(url, response);
                    throw new FindFeedException(
                        "The server at {0} returned an error.",
                        url.Host
                    );
                }

                var contentHeaders = response.Content.Headers;
                if (
                    contentHeaders.ContentType != null &&
                    contentHeaders.ContentType.CharSet != null
                )
                {
                    string charset = contentHeaders.ContentType.CharSet;
                    if (charset[0] == '"' && charset[charset.Length - 1] == '"')
                    {
                        charset = charset.Substring(1, charset.Length - 2);
                    }

                    contentHeaders.ContentType.CharSet = charset;
                }

                try
                {
                    return await response.Content.ReadAsStringAsync();
                }
                catch (InvalidOperationException)
                {
                    return String.Empty;
                }
            }
        }

        static int UrlFeedComparison(Uri x, Uri y)
        {
            // Reversed; if x's probability is larger it goes before y.
            return UrlFeedProbability(y) - UrlFeedProbability(x);
        }

        static int UrlFeedProbability(Uri url)
        {
            if (
                url.AbsoluteUri.IndexOf(
                    "comments",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0
            )
            {
                return -2;
            }

            if (
                url.AbsoluteUri.IndexOf(
                    "georss",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0
            )
            {
                return -1;
            }

            for (int i = 0; i < OrderedFeedKeywords.Length; i++)
            {
                string kw = OrderedFeedKeywords[i];
                if (
                    url.AbsoluteUri.IndexOf(
                        kw,
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                )
                {
                    return OrderedFeedKeywords.Length - i;
                }
            }

            return 0;
        }

        static async Task FilterUrlsByFeed(List<Uri> linkUrls)
        {
            bool[] results = await Task.WhenAll(linkUrls.Select(u => IsFeed(u)));
            for (int i = linkUrls.Count - 1; i >= 0; i--)
            {
                if (!results[i])
                {
                    linkUrls.RemoveAt(i);
                }
            }
        }

        static async Task<bool> IsFeed(Uri url)
        {
            try
            {
                string data = await GetFeedData(url);
                return LooksLikeFeed(data);
            }
            catch (FindFeedException)
            {
                return false;
            }
        }

        static bool LooksLikeFeed(string data)
        {
            data = data.ToLowerInvariant();
            if (data.IndexOf("<html") >= 0)
            {
                return false;
            }

            if (data.IndexOf("<rss") >= 0)
            {
                return true;
            }

            if (data.IndexOf("<rdf") >= 0)
            {
                return true;
            }

            if (data.IndexOf("<feed") >= 0)
            {
                return true;
            }

            return false;
        }

        static bool IsFeedishUrl(Uri hrefUrl)
        {
            for (int i = 0; i < OrderedFeedKeywords.Length; i++)
            {
                if (
                    hrefUrl.AbsoluteUri.IndexOf(
                        OrderedFeedKeywords[i],
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                )
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsFeedUrl(Uri hrefUrl)
        {
            for (int i = 0; i < FeedExtensions.Length; i++)
            {
                if (
                    hrefUrl.AbsolutePath.EndsWith(
                        FeedExtensions[i],
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }
    }
}
