using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using Newtonsoft.Json;

namespace onceandfuture
{
    static class Log
    {
        public static void BadDate(string url, string date)
        {
            Trace.TraceWarning("{0}: Unparsable date encountered: {1}", url, date);
        }

        public static void NetworkError(Uri uri, HttpRequestException requestException, Stopwatch loadTimer)
        {
            Trace.TraceError(
                "{0}: {2} ms: Network Error: {1}",
                uri,
                requestException.Message,
                loadTimer.ElapsedMilliseconds
            );
        }

        public static void XmlError(Uri uri, XmlException xmlException, Stopwatch loadTimer)
        {
            Trace.TraceError("{0}: {2} ms: XML Error: {1}", uri, xmlException.Message, loadTimer.ElapsedMilliseconds);
        }

        public static void BeginGetFeed(Uri uri)
        {
            Trace.TraceInformation("{0}: Begin fetching feed", uri);
        }

        public static void EndGetFeed(
            Uri uri,
            string version,
            HttpResponseMessage response,
            RiverFeed result,
            Stopwatch loadTimer
        )
        {
            Trace.TraceInformation(
                "{0}: Fetched {1} items from {2} feed in {3} ms",
                uri,
                result.Items.Count,
                version,
                loadTimer.ElapsedMilliseconds
            );
        }

        public static void UnrecognizableFeed(Uri uri, HttpResponseMessage response, string body, Stopwatch loadTimer)
        {
            Trace.TraceError(
                "{0}: Could not identify feed type in {1} ms: {2}",
                uri,
                loadTimer.ElapsedMilliseconds,
                body
            );
        }

        public static void EndGetFeedFailure(Uri uri, HttpResponseMessage response, string body, Stopwatch loadTimer)
        {
            Trace.TraceError(
                "{0}: Got failure status code {1} in {2} ms: {3}",
                uri,
                response.StatusCode,
                loadTimer.ElapsedMilliseconds,
                body
            );
        }

        public static void EndGetFeedNotModified(Uri uri, HttpResponseMessage response, Stopwatch loadTimer)
        {
            Trace.TraceInformation("{0}: Got feed not modified in {1} ms", uri, loadTimer.ElapsedMilliseconds);
        }

        public static void EndGetFeedMovedPermanently(Uri uri, HttpResponseMessage response, Stopwatch loadTimer)
        {
            Trace.TraceInformation(
                "{0}: Feed moved permanently to {2} in {1} ms",
                uri,
                loadTimer.ElapsedMilliseconds,
                response.Headers.Location);
        }

        public static void ConsideringImage(Uri baseUrl, Uri uri, string kind, int area, float ratio)
        {
            Trace.TraceInformation(
                "{0}: Considering image {1} ({2}) (area: {3}, ratio: {4})", baseUrl, uri, kind, area, ratio);
        }

        public static void NewBestImage(Uri baseUrl, Uri uri, string kind, int area, float ratio)
        {
            Trace.TraceInformation(
                "{0}: New best image: {1} ({2}) (area: {3}, ratio: {4})", baseUrl, uri, kind, area, ratio);
        }

        public static void ThumbnailErrorResponse(Uri baseUrl, Uri imageUri, string kind, HttpResponseMessage response)
        {
            Trace.TraceError(
                "{0}: {1} ({4}): Error From Host: {2} {3}",
                baseUrl,
                imageUri,
                response.StatusCode,
                response.ReasonPhrase,
                kind
            );
        }

        public static void InvalidThumbnailImageFormat(Uri baseUrl, Uri imageUri, string kind, ArgumentException ae)
        {
            Trace.TraceError("{0}: {1} ({2}): Is not a valid image ({3})", baseUrl, imageUri, kind, ae.Message);
        }

        public static void ThumbnailNetworkError(Uri baseUrl, Uri imageUri, string kind, HttpRequestException hre)
        {
            Trace.TraceError("{0}: {1} ({3}): Network Error: {2}", baseUrl, imageUri, hre.Message, kind);
        }

        public static void FoundThumbnail(Uri baseUrl, Uri uri, string kind)
        {
            Trace.TraceInformation("{0}: Found thumbnail {1} ({2})", baseUrl, uri, kind);
        }

        public static void BeginLoadThumbnails(RiverFeed feed)
        {
            Trace.TraceInformation(
                "{0}: Loading thumbnails...",
                feed.FeedUrl);
        }

        public static void EndLoadThumbnails(RiverFeed feed, RiverItem[] items, Stopwatch loadTimer)
        {
            Trace.TraceInformation(
                "{0}: Finished loading thumbs for {1} items in {2} ms",
                feed.FeedUrl,
                items.Length,
                loadTimer.ElapsedMilliseconds);
        }

        public static void NoThumbnailFound(Uri baseUrl)
        {
            Trace.TraceWarning("{0}: No suitable thumbnails found.", baseUrl);
        }

        public static void EndGetThumbsFromSoup(Uri baseUrl, int length, Stopwatch loadTimer)
        {
            Trace.TraceInformation(
                "{0}: Loaded {1} thumbnails in {2}ms", baseUrl, length, loadTimer.ElapsedMilliseconds);
        }

        public static void BeginGetThumbsFromSoup(Uri baseUrl, int length)
        {
            Trace.TraceInformation("{0}: Loading {1} thumbnails...", baseUrl, length);
        }

        public static void ThumbnailTooSmall(Uri baseUrl, Uri uri, string kind, int area)
        {
            Trace.TraceInformation("{0}: {1} ({2}): Too small ({3})", baseUrl, uri, kind, area);
        }

        public static void ThumbnailTooOblong(Uri baseUrl, Uri uri, string kind, float ratio)
        {
            Trace.TraceInformation("{0}: {1} ({2}): Too oblong ({3})", baseUrl, uri, kind, ratio);
        }

        public static void ThumbnailSuccessCacheHit(Uri baseUrl, Uri imageUrl)
        {
            Trace.TraceInformation("{0}: {1} Cached Success", baseUrl, imageUrl);
        }

        public static void ThumbnailErrorCacheHit(Uri baseUrl, Uri imageUrl, object cachedObject)
        {
            Trace.TraceInformation("{0}: {1} Cached Error: {2}", baseUrl, imageUrl, cachedObject);
        }

        public static void FeedTimeout(Uri uri, Stopwatch loadTimer)
        {
            Trace.TraceError("{0}: Timeout after {1}ms", uri, loadTimer.ElapsedMilliseconds);
        }

        public static void ThumbnailTimeout(Uri baseUrl, Uri imageUri, string kind)
        {
            Trace.TraceError("{0}: {1} ({2}): Timeout", baseUrl, imageUri, kind);
        }
    }

    public static class Util
    {
        readonly static Func<string, DateTime?>[] DateParsers = new Func<string, DateTime?>[]
        {
            TryParseDateNative,
            TryParseDateRFC822,
            TryParseDateAscTime,
            TryParseDateGreek,
            TryParseDateHungarian,
            TryParseDateIso8601,
            TryParseDateKorean,
            TryParseDatePerforce,
            TryParseDateW3DTF,
        };

        readonly static Dictionary<string, int> TimeZoneNames = new Dictionary<string, int>
        {
            { "ut", 0 },
            { "gmt", 0 },
            { "z", 0 },
            { "adt", -3 },
            { "ast", -4 },
            { "at", -4 },
            { "edt", -4 },
            { "est", -5 },
            { "et", -5 },
            { "cdt", -5 },
            { "cst", -6 },
            { "ct", -6 },
            { "mdt", -6 },
            { "mst", -7 },
            { "mt", -7 },
            { "pdt", -7 },
            { "pst", -8 },
            { "pt", -8 },
            { "a", -1 },
            { "n", 1 },
            { "m", -12 },
            { "y", 12 },
            { "met", 1 },
            { "mest", 2 },
        };

        readonly static HashSet<string> DayNames = new HashSet<string>
        {
            "mon", "tue", "wed", "thu", "fri", "sat", "sun"
        };

        readonly static Dictionary<string, int> Months = new Dictionary<string, int> {
            {"jan", 1}, {"feb", 2}, {"mar", 3}, {"apr", 4}, {"may", 5}, {"jun", 6},
            {"jul", 7}, {"aug", 8}, {"sep", 9}, {"oct", 10}, {"nov", 11}, {"dec", 12},
        };

        public static IEnumerable<TItem> ConcatSequence<TItem>(params IEnumerable<TItem>[] sequences)
        {
            return sequences.SelectMany(x => x);
        }

        public static string HashString(string input)
        {
            byte[] hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hash).Replace('/', '-');
        }

        public static string ParseBody(XElement body)
        {
            var parser = new HtmlParser();
            IHtmlDocument document = parser.Parse(body.Value);

            string result = HtmlFormatter.Format(document.Body);

            //string result = String.Join(
            //    " ",
            //    document.Body.Text().Split(default(char[]), StringSplitOptions.RemoveEmptyEntries)
            //);
            //if (result.Length > 280) { result = result.Substring(0, 277) + "..."; }
            return result;
        }

        public static DateTime? ParseDate(XElement dateTime)
        {
            for (int i = 0; i < DateParsers.Length; i++)
            {
                DateTime? result = DateParsers[i](dateTime.Value);
                if (result != null) { return result; }
            }

            Log.BadDate(dateTime.BaseUri, dateTime.Value);
            return null;
        }

        static string Slice(string str, int min, int max)
        {
            if (min >= str.Length) { return String.Empty; }
            if (max > str.Length) { max = str.Length; }
            return str.Substring(min, max - min);
        }

        static string ThreeChar(string str) => str.Length <= 3 ? str : str.Substring(0, 3);

        static DateTime? TryParseDateAscTime(string timeString)
        {
            // Like:
            //
            //    {weekday_name} {month_name} dd hh:mm:ss {+-tz} yyyy
            //    {weekday_name} {month_name} dd hh:mm:ss yyyy
            //
            List<string> parts = new List<string>(timeString.Split());
            if (parts.Count == 5) { parts.Insert(4, "+0000"); }
            if (parts.Count != 6) { return null; }
            return TryParseDateRFC822(String.Join(" ", parts[0], parts[2], parts[1], parts[5], parts[3], parts[4]));
        }

        static DateTime? TryParseDateGreek(string timeString) => null;

        static DateTime? TryParseDateHungarian(string timeString) => null;

        static DateTime? TryParseDateIso8601(string timeString) => null;

        static DateTime? TryParseDateKorean(string timeString) => null;

        static DateTime? TryParseDatePerforce(string timeString) => null;

        static DateTime? TryParseDateRFC822(string timeString)
        {
            List<string> parts = new List<string>(timeString.ToLowerInvariant().Split());
            if (parts.Count < 5)
            {
                // Assume that the time and TZ are missing.
                parts.Add("00:00:00");
                parts.Add("0000");
            }

            // If there's a day name at the front, remove it.            
            if (DayNames.Contains(ThreeChar(parts[0]))) { parts.RemoveAt(0); }

            // If we don't have at least 5 parts now there's not enough information to interpret.
            if (parts.Count < 5) { return null; }

            // Handle day and month name.
            int month = 0, day = 0;
            if (!(Months.TryGetValue(ThreeChar(parts[1]), out month) && int.TryParse(parts[0], out day)))
            {
                // Maybe month and day are swapped.
                if (!(Months.TryGetValue(ThreeChar(parts[0]), out month) && int.TryParse(parts[1], out day)))
                {
                    // Nope.
                    return null;
                }
            }
            if (month > 12) { return null; }
            if (day > 31) { return null; }

            // Year, including 2-digit years.
            int year = 0;
            if (!int.TryParse(parts[2], out year)) { return null; }
            if (year < 100) { year += (year < 90) ? 2000 : 1900; }
            if (year > 9999) { return null; }

            // Time.
            int hour = 0;
            int minute = 0;
            float second = 0;
            string[] timeParts = parts[3].Split(':');
            if (timeParts.Length > 0 && !int.TryParse(timeParts[0], out hour)) { return null; }
            if (timeParts.Length > 1 && !int.TryParse(timeParts[1], out minute)) { return null; }
            if (timeParts.Length > 2 && !float.TryParse(timeParts[2], out second)) { return null; }

            // Timezone
            if (parts[4].StartsWith("etc/")) { parts[4] = parts[4].Substring(4); }
            if (parts[4].StartsWith("gmt"))
            {
                // Normalize timezones that start with GMT: 
                // GMT-05:00 => -0500
                // GMT => GMT
                parts[4] = String.Join("", parts[4].Substring(3).Split(':'));
                if (parts[4].Length == 0) { parts[4] = "gmt"; }
            }

            // Handle timezones like '-0500', '+0500', and 'EST'
            int tz_hours = 0;
            int tz_minutes = 0;
            if (parts[4].Length >= 3 && (parts[4][0] == '-' || parts[4][0] == '+'))
            {
                if (!int.TryParse(Slice(parts[4], 1, 3), out tz_hours)) { return null; }
                if (parts[4].Length > 3)
                {
                    if (!int.TryParse(parts[4].Substring(3), out tz_minutes)) { return null; }
                }
            }
            else
            {
                if (!TimeZoneNames.TryGetValue(parts[4], out tz_hours)) { tz_hours = 0; }
            }

            // OK!
            try
            {
                return new DateTimeOffset(
                    year,
                    month,
                    day,
                    hour,
                    minute,
                    (int)second,
                    new TimeSpan(tz_hours, tz_minutes, 0)
                ).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        static DateTime? TryParseDateW3DTF(string timeString) => null;

        static DateTime? TryParseDateNative(string timeString)
        {
            DateTimeOffset result;
            if (DateTimeOffset.TryParse(timeString, out result))
            {
                return result.UtcDateTime;
            }
            return null;
        }

        class HtmlFormatter
        {
            readonly StringBuilder builder = new StringBuilder();

            HtmlFormatter() { }

            void AppendWhitespace()
            {
                if (builder.Length > 0 && !Char.IsWhiteSpace(builder[builder.Length - 1]))
                {
                    builder.Append(" ");
                }
            }


            public static string Format(IHtmlElement element)
            {
                var formatter = new HtmlFormatter();
                formatter.Visit(element);
                return formatter.builder.ToString();
            }

            bool Visit(INode node)
            {
                switch (node.NodeType)
                {
                case NodeType.Element:
                    if (node.NodeName == "SCRIPT") { break; }
                    if (node.NodeName == "FIGURE") { break; }

                    foreach (INode child in node.ChildNodes)
                    {
                        if (!Visit(child)) { return false; }
                    }
                    if (node.NodeName == "P" || node.NodeName == "DIV" || node.NodeName == "BR")
                    {
                        AppendWhitespace();
                    }
                    break;

                case NodeType.Text:
                case NodeType.CharacterData:
                case NodeType.EntityReference:
                    for (int i = 0; i < node.TextContent.Length && builder.Length < 280; i++)
                    {
                        if (Char.IsWhiteSpace(node.TextContent[i]))
                        {
                            AppendWhitespace();
                        }
                        else
                        {
                            builder.Append(node.TextContent[i]);
                        }
                    }
                    break;

                default:
                    break;
                }

                if (builder.Length > 280)
                {
                    builder.Length = 277;
                    builder.Append("...");
                    return false;
                }

                return true;
            }
        }
    }

    public static class XNames
    {
        public static class Content
        {
            public static readonly XNamespace Namespace = XNamespace.Get("http://purl.org/rss/1.0/modules/content/");

            public static readonly XName Encoded = Namespace.GetName("encoded");
        }

        public static class OPML
        {
            public static readonly XName Body = XName.Get("body");
            public static readonly XName Outline = XName.Get("outline");
            public static readonly XName HtmlUrl = XName.Get("htmlUrl");
            public static readonly XName Text = XName.Get("title");
            public static readonly XName Title = XName.Get("title");
            public static readonly XName Type = XName.Get("type");
            public static readonly XName Version = XName.Get("version");
            public static readonly XName XmlUrl = XName.Get("xmlUrl");
        }

        public static class RSS
        {
            public static readonly XName Title = XName.Get("title");
            public static readonly XName Link = XName.Get("link");
            public static readonly XName Comments = XName.Get("comments");
            public static readonly XName PubDate = XName.Get("pubDate");
            public static readonly XName Description = XName.Get("description");
            public static readonly XName Item = XName.Get("item");
            public static readonly XName Guid = XName.Get("guid");
            public static readonly XName Rss = XName.Get("rss");
            public static readonly XName Channel = XName.Get("channel");
            public static readonly XName IsPermaLink = XName.Get("isPermaLink");
            public static readonly XName Enclosure = XName.Get("length");
            public static readonly XName Length = XName.Get("length");
            public static readonly XName Type = XName.Get("type");
            public static readonly XName Url = XName.Get("url");
        }

        public static class Atom
        {
            public static readonly XNamespace Namespace = XNamespace.Get("http://www.w3.org/2005/Atom");
            public static readonly XName Feed = Namespace.GetName("feed");
            public static readonly XName Title = Namespace.GetName("title");
            public static readonly XName Id = Namespace.GetName("id");
            public static readonly XName Link = Namespace.GetName("link");
            public static readonly XName Summary = Namespace.GetName("summary");
            public static readonly XName Entry = Namespace.GetName("entry");
            public static readonly XName Content = Namespace.GetName("content");
            public static readonly XName Published = Namespace.GetName("published");
            public static readonly XName Updated = Namespace.GetName("updated");

            public static readonly XName Rel = XName.Get("rel");
            public static readonly XName Type = XName.Get("type");
            public static readonly XName Href = XName.Get("href");
            public static readonly XName Length = XName.Get("length");
        }

        public static class RDF
        {
            public static readonly XNamespace Namespace = XNamespace.Get("http://www.w3.org/1999/02/22-rdf-syntax-ns#");

            public static readonly XName Rdf = Namespace.GetName("RDF");
        }

        public static class RSS10
        {
            public static readonly XNamespace Namespace = XNamespace.Get("http://purl.org/rss/1.0/");

            public static readonly XName Title = Namespace.GetName("title");
            public static readonly XName Link = Namespace.GetName("link");
            public static readonly XName Comments = Namespace.GetName("comments");
            public static readonly XName PubDate = Namespace.GetName("pubDate");
            public static readonly XName Description = Namespace.GetName("description");
            public static readonly XName Item = Namespace.GetName("item");
            public static readonly XName Guid = Namespace.GetName("guid");
            public static readonly XName Rss = Namespace.GetName("rss");
            public static readonly XName Channel = Namespace.GetName("channel");
        }
    }

    class OpmlEntry
    {
        public OpmlEntry(
            string htmlUrl = null,
            string title = "",
            string text = "",
            string type = "",
            string version = "",
            Uri xmlUrl = null
        )
        {
            HtmlUrl = htmlUrl;
            Title = title;
            Text = text;
            Type = type;
            Version = version;
            XmlUrl = xmlUrl;
        }

        public static OpmlEntry FromXml(XElement element)
        {
            string xmlUrl = element.Attribute(XNames.OPML.XmlUrl)?.Value;

            return new OpmlEntry(
                htmlUrl: element.Attribute(XNames.OPML.HtmlUrl)?.Value,
                title: element.Attribute(XNames.OPML.Title)?.Value,
                text: element.Attribute(XNames.OPML.Text)?.Value,
                type: element.Attribute(XNames.OPML.Type)?.Value,
                version: element.Attribute(XNames.OPML.Version)?.Value,
                xmlUrl: xmlUrl != null ? new Uri(xmlUrl) : null
            );
        }

        public string HtmlUrl { get; }
        public string Title { get; }
        public string Text { get; }
        public string Type { get; }
        public string Version { get; }
        public Uri XmlUrl { get; }
    }

    [JsonObject]
    public class RiverFeed
    {
        public RiverFeed(
            RiverFeed otherFeed = null,
            string feedTitle = null,
            Uri feedUrl = null,
            string websiteUrl = null,
            string feedDescription = null,
            DateTime? whenLastUpdate = null,
            IEnumerable<RiverItem> items = null)
        {
            FeedTitle = feedTitle ?? otherFeed?.FeedTitle ?? String.Empty;
            FeedUrl = feedUrl ?? otherFeed?.FeedUrl;
            WebsiteUrl = websiteUrl ?? otherFeed?.WebsiteUrl ?? String.Empty;
            FeedDescription = feedDescription ?? otherFeed?.FeedDescription ?? String.Empty;
            WhenLastUpdate = whenLastUpdate ?? otherFeed?.WhenLastUpdate ?? DateTime.UtcNow;

            Items = ImmutableList.CreateRange<RiverItem>(items ?? otherFeed?.Items ?? Enumerable.Empty<RiverItem>());
        }

        [JsonProperty(PropertyName = "feedTitle")]
        public string FeedTitle { get; }

        [JsonProperty(PropertyName = "feedUrl")]
        public Uri FeedUrl { get; }

        [JsonProperty(PropertyName = "websiteUrl")]
        public string WebsiteUrl { get; }

        [JsonProperty(PropertyName = "feedDescription")]
        public string FeedDescription { get; }

        [JsonProperty(PropertyName = "whenLastUpdate")]
        public DateTime WhenLastUpdate { get; }

        [JsonProperty(PropertyName = "item")]
        public ImmutableList<RiverItem> Items { get; }
    }

    // TODO: Relative URLs.
    // TODO: Timeouts

    public class RiverItem
    {
        [JsonConstructor]
        public RiverItem(
            RiverItem existingItem = null,
            string title = null,
            string link = null,
            string body = null,
            DateTime? pubDate = null,
            string permaLink = null,
            string comments = null,
            string id = null,
            RiverItemThumbnail thumbnail = null,
            IEnumerable<RiverItemEnclosure> enclosures = null)
        {
            Title = title ?? existingItem?.Title ?? String.Empty;
            Link = link ?? existingItem?.Link ?? String.Empty;
            Body = body ?? existingItem?.Body ?? String.Empty;
            PubDate = pubDate ?? existingItem?.PubDate;
            PermaLink = permaLink ?? existingItem?.PermaLink;
            Comments = comments ?? existingItem?.Comments;
            Id = id ?? existingItem?.Id;
            Thumbnail = thumbnail ?? existingItem?.Thumbnail;

            Enclosures = ImmutableList.CreateRange<RiverItemEnclosure>(
                enclosures ??
                existingItem?.Enclosures ??
                Enumerable.Empty<RiverItemEnclosure>()
            );
        }

        [JsonProperty(PropertyName = "title")]
        public string Title { get; }

        [JsonProperty(PropertyName = "link")]
        public string Link { get; }

        [JsonProperty(PropertyName = "body")]
        public string Body { get; }

        [JsonProperty(PropertyName = "pubDate")]
        public DateTime? PubDate { get; }

        [JsonProperty(PropertyName = "permaLink")]
        public string PermaLink { get; }

        [JsonProperty(PropertyName = "comments")]
        public string Comments { get; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; }

        [JsonProperty(PropertyName = "thumbnail")]
        public RiverItemThumbnail Thumbnail { get; }

        [JsonProperty(PropertyName = "enclosure")]
        public ImmutableList<RiverItemEnclosure> Enclosures { get; }
    }

    public class RiverItemThumbnail
    {
        public RiverItemThumbnail(
            RiverItemThumbnail existingThumbnail = null,
            string url = null,
            int? width = 0,
            int? height = 0)
        {
            Url = url ?? existingThumbnail?.Url;
            Width = width ?? existingThumbnail?.Width ?? 0;
            Height = height ?? existingThumbnail?.Height ?? 0;
        }

        [JsonProperty(PropertyName = "url")]
        public string Url { get; }

        [JsonProperty(PropertyName = "width")]
        public int Width { get; }

        [JsonProperty(PropertyName = "height")]
        public int Height { get; }
    }

    public class RiverItemEnclosure
    {
        public RiverItemEnclosure(
            RiverItemEnclosure existingEnclosure = null,
            string url = null,
            string type = null,
            string length = null)
        {
            Url = url ?? existingEnclosure?.Url;
            Type = type ?? existingEnclosure?.Type;
            Length = length ?? existingEnclosure?.Length;
        }

        [JsonProperty(PropertyName = "url")]
        public string Url { get; }

        [JsonProperty(PropertyName = "type")]
        public string Type { get; }

        [JsonProperty(PropertyName = "length")]
        public string Length { get; }
    }

    public class RiverFeedMeta
    {
        public RiverFeedMeta(
            RiverFeedMeta existingMeta = null,
            string name = null,
            Uri originUrl = null,
            string docs = null,
            string etag = null,
            DateTimeOffset? lastModified = null,
            HttpStatusCode? lastStatus = null)
        {
            Name = name ?? existingMeta?.Name;
            OriginUrl = originUrl ?? existingMeta?.OriginUrl;
            Docs = docs ?? existingMeta?.Docs ?? "http://riverjs.org/";
            Etag = etag ?? existingMeta?.Etag;
            LastModified = lastModified ?? existingMeta?.LastModified;
            LastStatus = lastStatus ?? existingMeta?.LastStatus ?? HttpStatusCode.OK;
        }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; }

        [JsonProperty(PropertyName = "originUrl")]
        public Uri OriginUrl { get; }

        [JsonProperty(PropertyName = "docs")]
        public string Docs { get; }

        [JsonProperty(PropertyName = "etag")]
        public string Etag { get; }

        [JsonProperty(PropertyName = "lastModified")]
        public DateTimeOffset? LastModified { get; }

        [JsonProperty(PropertyName = "lastStatus")]
        public HttpStatusCode LastStatus { get; }
    }

    public class UpdatedFeeds
    {
        public UpdatedFeeds(
            UpdatedFeeds existingFeeds = null,
            IEnumerable<RiverFeed> feeds = null)
        {
            Feeds = ImmutableList.CreateRange<RiverFeed>(
                feeds ?? existingFeeds?.Feeds ?? Enumerable.Empty<RiverFeed>()
            );
        }

        [JsonProperty(PropertyName = "updatedFeed")]
        public ImmutableList<RiverFeed> Feeds { get; }
    }

    public class River
    {
        public River(
            River existingRiver = null,
            UpdatedFeeds updatedFeeds = null,
            RiverFeedMeta metadata = null)
        {
            UpdatedFeeds = updatedFeeds ?? existingRiver?.UpdatedFeeds ?? new UpdatedFeeds();
            Metadata = metadata ?? existingRiver?.Metadata ?? new RiverFeedMeta();
        }

        [JsonProperty(PropertyName = "updatedFeeds")]
        public UpdatedFeeds UpdatedFeeds { get; }

        [JsonProperty(PropertyName = "metadata")]
        public RiverFeedMeta Metadata { get; }
    }

    class ImageData
    {
        public ImageData(int width, int height, byte[] data)
        {
            Width = width;
            Height = height;
            Data = data;
        }
        public int Width { get; }
        public int Height { get; }
        public byte[] Data { get; }
    }

    static class ThumbnailExtractor
    {
        static readonly HttpClient client;
        static readonly MemoryCache imageCache;

        static readonly string[] BadThumbnails = new string[]
        {
            "addgoogle2.gif",
            "blank.jpg",
            "spacer.gif",
        };

        static readonly string[] BadThumbnailHosts = new string[]
        {
            "amazon-adsystem.com",
            "doubleclick.net",
            "googleadservices.com",
            "gravatar.com",
            "pixel.quantserve.com",
        };

        static ThumbnailExtractor()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TheOnceAndFuture/1.0");

            imageCache = new MemoryCache("ThumbMemoryCache", new NameValueCollection
            {
                { "cacheMemoryLimitMegabytes", "100" },
                { "physicalMemoryLimitPercentage", "10" },
            });
        }

        // TODO: Thumbs only for new items.

        public static async Task<RiverItem> GetItemThumbnailAsync(RiverItem item, Uri baseUri, CancellationToken token)
        {
            if (item.Thumbnail != null) { return item; }
            if (item.Link == null) { return item; }

            Uri itemLink;
            if (!Uri.TryCreate(item.Link, UriKind.RelativeOrAbsolute, out itemLink)) { return item; }
            if (!itemLink.IsAbsoluteUri)
            {
                Uri relativeUri = itemLink;
                if (!Uri.TryCreate(relativeUri, baseUri, out itemLink)) { return item; }
            }

            ImageData sourceImage = await FindThumbnailAsync(itemLink, token);
            if (sourceImage == null) { return item; }
            ImageData thumbnail = MakeThumbnail(sourceImage);

            Uri thumbnailUri = await RiverThumbnailStore.StoreImage(thumbnail);
            return new RiverItem(
                item,
                thumbnail: new RiverItemThumbnail(
                    url: thumbnailUri.AbsoluteUri,
                    width: thumbnail.Width,
                    height: thumbnail.Height
                )
            );
        }

        private static ImageData MakeThumbnail(ImageData sourceImage)
        {
            // TODO: Crop square using entropy, &c.
            return sourceImage;
        }

        static async Task<ImageData> FindThumbnailAsync(Uri uri, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await client.GetAsync(uri);
            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    string mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                    if (mediaType.Contains("image"))
                    {
                        var iu = new ImageUrl { Uri = uri, Kind = "Direct" };
                        return await FetchThumbnailAsync(iu, uri, cancellationToken);
                    }

                    if (mediaType.Contains("html"))
                    {
                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                        {
                            var parser = new HtmlParser();
                            IHtmlDocument document = await parser.ParseAsync(stream);

                            return await FindThumbnailInSoupAsync(uri, document, cancellationToken);
                        }
                    }
                }
            }

            return null;
        }

        static async Task<ImageData> FindThumbnailInSoupAsync(
            Uri baseUrl, IHtmlDocument document, CancellationToken cancellationToken)
        {
            // These get preferential treatment; if we find them then great otherwise we have to search the whole doc.
            // (Note that they also still have to pass the URL filter.)
            ImageUrl easyUri = Util.ConcatSequence(
                ExtractOpenGraphImageUrls(baseUrl, document),
                ExtractTwitterImageUrls(baseUrl, document),
                ExtractLinkRelImageUrls(baseUrl, document),
                ExtractKnownGoodnessImageUrls(baseUrl, document)
            ).FirstOrDefault();

            if (easyUri != null)
            {
                Log.FoundThumbnail(baseUrl, easyUri.Uri, easyUri.Kind);
                return await FetchThumbnailAsync(easyUri, baseUrl, cancellationToken);
            }

            IEnumerable<Uri> distinctSrc =
                (from element in document.GetElementsByTagName("img")
                 let src = MakeThumbnailUrl(baseUrl, element.Attributes["src"]?.Value)
                 where src != null
                 select src).Distinct();

            ImageUrl[] imageUrls =
                (from src in distinctSrc
                 select new ImageUrl { Uri = src, Kind = "ImgTag" }).ToArray();

            Stopwatch loadTimer = Stopwatch.StartNew();
            Log.BeginGetThumbsFromSoup(baseUrl, imageUrls.Length);
            var potentialThumbnails = new Task<ImageData>[imageUrls.Length];
            for (int i = 0; i < potentialThumbnails.Length; i++)
            {
                potentialThumbnails[i] = FetchThumbnailAsync(imageUrls[i], baseUrl, cancellationToken);
            }

            ImageData[] images = await Task.WhenAll(potentialThumbnails);
            Log.EndGetThumbsFromSoup(baseUrl, imageUrls.Length, loadTimer);

            ImageUrl bestImageUrl = null;
            ImageData bestImage = null;
            int bestArea = 0;
            for (int i = 0; i < images.Length; i++)
            {
                ImageUrl imageUrl = imageUrls[i];
                ImageData image = images[i];
                if (image == null) { continue; } // It was invalid.

                int width = image.Width;
                int height = image.Height;
                int area = width * height;
                if (area < 5000)
                {
                    Log.ThumbnailTooSmall(baseUrl, imageUrl.Uri, imageUrl.Kind, area);
                    CacheError(imageUrl, "Too Small");
                    continue;
                }

                float ratio = (float)Math.Max(width, height) / (float)Math.Min(width, height);
                if (ratio > 2.25f)
                {
                    Log.ThumbnailTooOblong(baseUrl, imageUrl.Uri, imageUrl.Kind, ratio);
                    CacheError(imageUrl, "Too Oblong");
                    continue;
                }

                if (imageUrl.Uri.AbsolutePath.Contains("sprite")) { ratio /= 10; } // Penalize images named "sprite"

                Log.ConsideringImage(baseUrl, imageUrl.Uri, imageUrl.Kind, area, ratio);
                if (ratio > bestArea)
                {
                    bestArea = area;
                    bestImage = image;
                    bestImageUrl = imageUrls[i];
                    Log.NewBestImage(baseUrl, bestImageUrl.Uri, bestImageUrl.Kind, area, ratio);
                }
            }

            if (bestImage != null)
            {
                Log.FoundThumbnail(baseUrl, bestImageUrl.Uri, bestImageUrl.Kind);
            }
            else
            {
                Log.NoThumbnailFound(baseUrl);
            }
            return bestImage;
        }

        static IEnumerable<ImageUrl> ExtractKnownGoodnessImageUrls(Uri baseUrl, IHtmlDocument document)
        {
            IElement element = document.QuerySelector("section.comic-art");
            if (element != null)
            {
                Uri uri = MakeThumbnailUrl(baseUrl, element.QuerySelector("img")?.GetAttribute("src"));
                if (uri != null) { yield return new ImageUrl { Uri = uri, Kind = "KnownGood" }; }
            }
        }

        static IEnumerable<ImageUrl> ExtractLinkRelImageUrls(Uri baseUrl, IHtmlDocument document)
        {
            return
                from element in document.All
                where element.LocalName == "link"
                where element.Attributes["rel"]?.Value == "image_src"
                let thumbnail = MakeThumbnailUrl(baseUrl, element.Attributes["href"]?.Value)
                where thumbnail != null
                select new ImageUrl { Uri = thumbnail, Kind = "RelImage" };
        }

        static IEnumerable<ImageUrl> ExtractTwitterImageUrls(Uri baseUrl, IHtmlDocument document)
        {
            return
                from element in document.All
                where element.LocalName == "meta"
                where
                    element.Attributes["name"]?.Value == "twitter:image" ||
                    element.Attributes["property"]?.Value == "twitter:image"
                let thumbnail = MakeThumbnailUrl(baseUrl, element.Attributes["content"]?.Value)
                where thumbnail != null
                select new ImageUrl { Uri = thumbnail, Kind = "TwitterImage" };
        }

        static IEnumerable<ImageUrl> ExtractOpenGraphImageUrls(Uri baseUrl, IHtmlDocument document)
        {
            return
                from element in document.All
                where element.LocalName == "meta"
                where
                    element.Attributes["name"]?.Value == "og:image" ||
                    element.Attributes["property"]?.Value == "og:image" ||
                    element.Attributes["name"]?.Value == "og:image:url" ||
                    element.Attributes["property"]?.Value == "og:image:url"
                let thumbnail = MakeThumbnailUrl(baseUrl, element.Attributes["content"]?.Value)
                where thumbnail != null
                select new ImageUrl { Uri = thumbnail, Kind = "OpenGraph" };
        }

        static readonly TimeSpan ErrorCacheLifetime = TimeSpan.FromSeconds(30);
        static readonly TimeSpan SuccessCacheLifetime = TimeSpan.FromHours(1);

        static async Task<ImageData> FetchThumbnailAsync(
            ImageUrl imageUrl,
            Uri referrer,
            CancellationToken cancellationToken)
        {            
            try
            {
                object cachedObject = imageCache.Get(imageUrl.Uri.AbsoluteUri);
                if (cachedObject is string)
                {
                    Log.ThumbnailErrorCacheHit(referrer, imageUrl.Uri, cachedObject);
                    return null;
                }
                if (cachedObject is ImageData)
                {
                    Log.ThumbnailSuccessCacheHit(referrer, imageUrl.Uri);
                    return (ImageData)cachedObject;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, imageUrl.Uri);
                if (referrer != null) { request.Headers.Referrer = referrer; }

                HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Log.ThumbnailErrorResponse(referrer, imageUrl.Uri, imageUrl.Kind, response);
                    CacheError(imageUrl, response.ReasonPhrase);
                    return null;
                }

                byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                using (var stream = new MemoryStream(imageBytes))
                {
                    try
                    {
                        using (Image streamImage = Image.FromStream(stream))
                        {
                            return CacheSuccess(
                                imageUrl, new ImageData(streamImage.Width, streamImage.Height, imageBytes));
                        }
                    }
                    catch (ArgumentException ae)
                    {
                        Log.InvalidThumbnailImageFormat(referrer, imageUrl.Uri, imageUrl.Kind, ae);
                        CacheError(imageUrl, ae.Message);
                        return null;
                    }
                }
            }
            catch(TaskCanceledException tce)
            {
                Log.ThumbnailTimeout(referrer, imageUrl.Uri, imageUrl.Kind);
                CacheError(imageUrl, tce.Message);
                return null;
            }
            catch (HttpRequestException hre)
            {
                Log.ThumbnailNetworkError(referrer, imageUrl.Uri, imageUrl.Kind, hre);
                CacheError(imageUrl, hre.Message);
                return null;
            }
        }

        static ImageData CacheSuccess(ImageUrl imageUrl, ImageData image)
        {
            // Bypass the cache if the image is too big.
            if (image.Width * image.Height >= 5000) { return image; }

            CacheItem existing = imageCache.AddOrGetExisting(
                new CacheItem(imageUrl.Uri.AbsoluteUri, image),
                new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.UtcNow + SuccessCacheLifetime });
            if (existing == null) { return image; }

            // N.B.: If we raced with another success this will just return the successful image. If we raced with
            //       a failure this will return null, as appropriate.
            return existing.Value as ImageData;
        }

        static void CacheError(ImageUrl imageUrl, string message)
        {
            CacheItem existing = imageCache.AddOrGetExisting(
                new CacheItem(imageUrl.Uri.AbsoluteUri, message),
                new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.UtcNow + ErrorCacheLifetime });
            if (existing != null)
            {
                existing.Value = message;
            }
        }

        static Uri MakeThumbnailUrl(Uri baseUrl, string src)
        {
            Uri thumbnail;

            if (String.IsNullOrWhiteSpace(src)) { return null; }
            if (!Uri.TryCreate(src, UriKind.RelativeOrAbsolute, out thumbnail)) { return null; }
            if (!thumbnail.IsAbsoluteUri)
            {
                Uri relativeUrl = thumbnail;
                if (!Uri.TryCreate(baseUrl, relativeUrl, out thumbnail)) { return null; }
            }

            for (int i = 0; i < BadThumbnails.Length; i++)
            {
                if (thumbnail.AbsolutePath.EndsWith(BadThumbnails[i], StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            for (int i = 0; i < BadThumbnailHosts.Length; i++)
            {
                if (thumbnail.Host.EndsWith(BadThumbnailHosts[i], StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return thumbnail;
        }

        class ImageUrl
        {
            public string Kind;
            public Uri Uri;
        }
    }

    class FetchResult
    {
        public FetchResult(
            RiverFeed feed,
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

        public RiverFeed Feed { get; }
        public HttpStatusCode Status { get; }
        public Uri FeedUrl { get; }
        public string Etag { get; }
        public DateTimeOffset? LastModified { get; }
    }

    static class RiverFeedParser
    {
        static readonly HttpClient client;

        static readonly Dictionary<XName, Func<RiverFeed, XElement, RiverFeed>> FeedElements =
            new Dictionary<XName, Func<RiverFeed, XElement, RiverFeed>>
            {
                { XNames.RSS.Title,       (rf, xe) => new RiverFeed(rf, feedTitle: Util.ParseBody(xe)) },
                { XNames.RSS10.Title,     (rf, xe) => new RiverFeed(rf, feedTitle: Util.ParseBody(xe)) },
                { XNames.Atom.Title,      (rf, xe) => new RiverFeed(rf, feedTitle: Util.ParseBody(xe)) },

                { XNames.RSS.Link,        (rf, xe) => new RiverFeed(rf, websiteUrl: xe.Value) },
                { XNames.RSS10.Link,      (rf, xe) => new RiverFeed(rf, websiteUrl: xe.Value) },
                { XNames.Atom.Link,       (rf, xe) => HandleAtomLink(rf, xe) },

                { XNames.RSS.Description,   (rf, xe) => new RiverFeed(rf, feedDescription: Util.ParseBody(xe)) },
                { XNames.RSS10.Description, (rf, xe) => new RiverFeed(rf, feedDescription: Util.ParseBody(xe)) },

                { XNames.RSS.Item,        (rf, xe) => new RiverFeed(rf, items: rf.Items.Add(LoadItem(xe))) },
                { XNames.RSS10.Item,      (rf, xe) => new RiverFeed(rf, items: rf.Items.Add(LoadItem(xe))) },
                { XNames.Atom.Entry,      (rf, xe) => new RiverFeed(rf, items: rf.Items.Add(LoadItem(xe))) },
            };

        static readonly Dictionary<XName, Func<RiverItem, XElement, RiverItem>> ItemElements =
            new Dictionary<XName, Func<RiverItem, XElement, RiverItem>>
            {
                { XNames.RSS.Title,       (ri, xe) => new RiverItem(ri, title: Util.ParseBody(xe)) },
                { XNames.RSS.Link,        (ri, xe) => new RiverItem(ri, link: xe.Value) },
                { XNames.RSS.Description, (ri, xe) => new RiverItem(ri, body: Util.ParseBody(xe)) },
                { XNames.RSS.Comments,    (ri, xe) => new RiverItem(ri, comments: xe.Value) },
                { XNames.RSS.PubDate,     (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.RSS.Guid,        (ri, xe) => HandleGuid(ri, xe) },
                { XNames.RSS.Enclosure,   (ri, xe) => HandleEnclosure(ri, xe) },

                { XNames.RSS10.Title,       (ri, xe) => new RiverItem(ri, title: Util.ParseBody(xe)) },
                { XNames.RSS10.Link,        (ri, xe) => new RiverItem(ri, link: xe.Value) },
                { XNames.RSS10.Description, (ri, xe) => new RiverItem(ri, body: Util.ParseBody(xe)) },
                { XNames.RSS10.Comments,    (ri, xe) => new RiverItem(ri, comments: xe.Value) },
                { XNames.RSS10.PubDate,     (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.RSS10.Guid,        (ri, xe) => HandleGuid(ri, xe) },

                { XNames.Content.Encoded,  (ri, xe) => new RiverItem(ri, body: Util.ParseBody(xe)) },

                { XNames.Atom.Title,       (ri, xe) => new RiverItem(ri, title: Util.ParseBody(xe)) },
                { XNames.Atom.Content,     (ri, xe) => new RiverItem(ri, body: Util.ParseBody(xe)) },
                { XNames.Atom.Summary,     (ri, xe) => new RiverItem(ri, body: Util.ParseBody(xe)) },
                { XNames.Atom.Link,        (ri, xe) => HandleAtomLink(ri, xe) },
                { XNames.Atom.Id,          (ri, xe) => new RiverItem(ri, id: xe.Value) },
                { XNames.Atom.Published,   (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.Atom.Updated,     (ri, xe) => HandlePubDate(ri, xe) },
        };

        static RiverFeedParser()
        {
            // TODO: Caching.
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;

            client = new HttpClient(httpClientHandler, false);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TheOnceAndFuture/1.0");
        }

        public static async Task<FetchResult> FetchAsync(
            Uri uri,
            string etag,
            DateTimeOffset? lastModified,
            CancellationToken cancellationToken
        )
        {
            Stopwatch loadTimer = Stopwatch.StartNew();
            try
            {
                Log.BeginGetFeed(uri);
                HttpResponseMessage response = null;

                Uri requestUri = uri;
                for (int i = 0; i < 30; i++)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                    if (etag != null) { request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag)); }
                    request.Headers.IfModifiedSince = lastModified;

                    response = await client.SendAsync(request, cancellationToken);
                    if ((response.StatusCode != HttpStatusCode.TemporaryRedirect) &&
                        (response.StatusCode != HttpStatusCode.Found) &&
                        (response.StatusCode != HttpStatusCode.SeeOther))
                    {
                        break;
                    }

                    requestUri = response.Headers.Location;
                    Console.WriteLine("Redirect {0} ==> {1}", uri, requestUri);
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
                    return new FetchResult(
                        feed: null,
                        status: HttpStatusCode.MovedPermanently,
                        feedUrl: response.Headers.Location,
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

                // TODO: Character detection!

                await response.Content.LoadIntoBufferAsync();
                using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                using (var textReader = new StreamReader(responseStream))
                using (var reader = XmlReader.Create(textReader, null, responseUri.AbsoluteUri))
                {
                    RiverFeed result = null;
                    XElement element = XElement.Load(reader, LoadOptions.SetBaseUri);
                    if (element.Name == XNames.RSS.Rss)
                    {
                        result = LoadFeed(responseUri, element.Element(XNames.RSS.Channel));
                        Log.EndGetFeed(uri, "rss2.0", response, result, loadTimer);
                    }
                    else if (element.Name == XNames.Atom.Feed)
                    {
                        result = LoadFeed(responseUri, element);
                        Log.EndGetFeed(uri, "atom", response, result, loadTimer);
                    }
                    else if (element.Name == XNames.RDF.Rdf)
                    {
                        result = LoadFeed(responseUri, element.Element(XNames.RSS10.Channel));
                        result = new RiverFeed(
                            result,
                            items: result.Items.AddRange(
                                element.Elements(XNames.RSS10.Item).Select(xe => LoadItem(xe))
                            )
                        );
                        Log.EndGetFeed(uri, "rdf", response, result, loadTimer);
                    }
                    else
                    {
                        Log.UnrecognizableFeed(uri, response, element.ToString(), loadTimer);
                    }

                    string newEtag = response.Headers.ETag?.Tag;
                    DateTimeOffset? newLastModified = response.Content.Headers.LastModified;

                    result = await LoadItemThumbnails(result, cancellationToken);

                    return new FetchResult(
                        feed: result,
                        status: HttpStatusCode.OK,
                        feedUrl: responseUri,
                        etag: newEtag,
                        lastModified: newLastModified);
                }
            }
            catch (TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested) { throw; }
                Log.FeedTimeout(uri, loadTimer);
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

        static RiverFeed HandleAtomLink(RiverFeed feed, XElement link)
        {
            if (link.Attribute(XNames.Atom.Rel) == null)
            {
                feed = new RiverFeed(feed, websiteUrl: link.Attribute(XNames.Atom.Href)?.Value);
            }

            if (
                link.Attribute(XNames.Atom.Rel)?.Value == "alternate"
                && link.Attribute(XNames.Atom.Type)?.Value == "text/html"
            )
            {
                feed = new RiverFeed(feed, websiteUrl: link.Attribute(XNames.Atom.Href)?.Value);
            }

            return feed;
        }

        static RiverItem HandleAtomLink(RiverItem item, XElement link)
        {
            if (link.Attribute(XNames.Atom.Rel) == null)
            {
                item = new RiverItem(item, link: link.Attribute(XNames.Atom.Href)?.Value);
            }

            if (link.Attribute(XNames.Atom.Rel)?.Value == "alternate" &&
                link.Attribute(XNames.Atom.Type)?.Value == "text/html")
            {
                item = new RiverItem(item, link: link.Attribute(XNames.Atom.Href)?.Value);
            }

            if (link.Attribute(XNames.Atom.Rel)?.Value == "self" &&
                link.Attribute(XNames.Atom.Type)?.Value == "text/html")
            {
                item = new RiverItem(item, permaLink: link.Attribute(XNames.Atom.Href)?.Value);
            }

            if (link.Attribute(XNames.Atom.Rel)?.Value == "enclosure")
            {
                item = new RiverItem(item, enclosures: item.Enclosures.Add(new RiverItemEnclosure(
                    length: link.Attribute(XNames.Atom.Length)?.Value,
                    type: link.Attribute(XNames.Atom.Type)?.Value,
                    url: link.Attribute(XNames.Atom.Href)?.Value
                )));
            }
            return item;
        }

        static RiverItem HandleEnclosure(RiverItem item, XElement element)
        {
            return new RiverItem(
                item,
                enclosures: item.Enclosures.Add(new RiverItemEnclosure(
                    length: element.Attribute(XNames.RSS.Length)?.Value,
                    type: element.Attribute(XNames.RSS.Type)?.Value,
                    url: element.Attribute(XNames.RSS.Url)?.Value
                )));
        }

        static RiverItem HandleGuid(RiverItem item, XElement element)
        {
            item = new RiverItem(item, id: element.Value);
            if (item.Id.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                item.Id.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                item = new RiverItem(item, permaLink: item.Id);
            }
            return item;
        }

        static RiverItem HandlePubDate(RiverItem item, XElement element)
        {
            DateTime? date = Util.ParseDate(element);
            if (date != null && (item.PubDate == null || date > item.PubDate))
            {
                return new RiverItem(item, pubDate: date);
            }
            return item;
        }

        static RiverFeed LoadFeed(Uri feedUrl, XElement item)
        {
            var rf = new RiverFeed(feedUrl: feedUrl);
            foreach (XElement xe in item.Elements())
            {
                Func<RiverFeed, XElement, RiverFeed> action;
                if (FeedElements.TryGetValue(xe.Name, out action)) { rf = action(rf, xe); }
            }
            if (String.IsNullOrWhiteSpace(rf.FeedTitle))
            {
                string title = null;
                if (!String.IsNullOrWhiteSpace(rf.FeedDescription)) { title = rf.FeedDescription; }
                else if (!String.IsNullOrWhiteSpace(rf.WebsiteUrl)) { title = rf.WebsiteUrl; }
                else if (rf.FeedUrl != null) { title = rf.FeedUrl.AbsoluteUri; }

                rf = new RiverFeed(rf, feedTitle: title);
            }
            return rf;
        }

        static RiverItem LoadItem(XElement item)
        {
            var ri = new RiverItem();
            foreach (XElement xe in item.Elements())
            {
                Func<RiverItem, XElement, RiverItem> func;
                if (ItemElements.TryGetValue(xe.Name, out func)) { ri = func(ri, xe); }
            }

            if (ri.PermaLink == null) { ri = new RiverItem(ri, permaLink: ri.Link); }
            if (ri.Id == null) { ri = new RiverItem(ri, id: CreateItemId(ri)); }
            if (String.IsNullOrWhiteSpace(ri.Title))
            {
                string title = null;
                if (ri.PubDate != null) { title = ri.PubDate.ToString(); }
                else if (ri.PermaLink != null) { title = ri.PermaLink; }
                else if (ri.Id != null) { title = ri.Id; }

                if (title != null) { ri = new RiverItem(ri, title: title); }
            }
            return ri;
        }

        static string CreateItemId(RiverItem item)
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

        static async Task<RiverFeed> LoadItemThumbnails(RiverFeed feed, CancellationToken token)
        {
            if (feed == null) { return null; }

            Stopwatch loadTimer = Stopwatch.StartNew();
            Log.BeginLoadThumbnails(feed);
            Task<RiverItem>[] itemTasks =
                (from item in feed.Items
                 select ThumbnailExtractor.GetItemThumbnailAsync(item, feed.FeedUrl, token)).ToArray();

            RiverItem[] items = await Task.WhenAll(itemTasks);
            Log.EndLoadThumbnails(feed, items, loadTimer);
            return new RiverFeed(feed, items: items);
        }

    }

    static class RiverThumbnailStore
    {
        public static async Task<Uri> StoreImage(ImageData image)
        {
            MemoryStream stream = new MemoryStream();
            using (Image source = Image.FromStream(new MemoryStream(image.Data)))
            {
                source.Save(stream, ImageFormat.Png);
            }

            stream.Position = 0;
            byte[] hash = SHA1.Create().ComputeHash(stream);
            string fileName = Convert.ToBase64String(hash).Replace('/', '-') + ".png";
            for (int i = 0; i < 3; i++)
            {
                if (File.Exists(fileName)) { break; }
                try
                {
                    using (FileStream outputFile = File.Create(fileName))
                    {
                        stream.Position = 0;
                        await stream.CopyToAsync(outputFile);
                    }
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(100);
                }
            }

            return new Uri(Path.GetFullPath(fileName));
        }
    }

    static class RiverFeedStore
    {
        static string GetNameForUri(Uri feedUri)
        {
            // TODO: Normalize URI?
            return Util.HashString(feedUri.AbsoluteUri);
        }

        public static async Task<River> LoadRiverForFeed(Uri feedUri)
        {
            try
            {
                using (var stream = File.OpenText(GetNameForUri(feedUri)))
                {
                    string text = await stream.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<River>(text);
                }
            }
            catch (FileNotFoundException)
            {
                return new River(metadata: new RiverFeedMeta(originUrl: feedUri));
            }
        }

        public static async Task WriteRiver(Uri uri, River river)
        {
            using (var stream = File.CreateText(GetNameForUri(uri)))
            {
                await stream.WriteAsync(JsonConvert.SerializeObject(river));
            }
        }
    }

    class Program
    {
        public static async Task<River> UpdateRiver(River river, CancellationToken cancellationToken)
        {
            FetchResult fetchResult = await RiverFeedParser.FetchAsync(
                river.Metadata.OriginUrl,
                river.Metadata.Etag,
                river.Metadata.LastModified,
                cancellationToken
            );

            var updatedFeeds = river.UpdatedFeeds;
            var metadata = new RiverFeedMeta(
                river.Metadata,
                etag: fetchResult.Etag,
                lastModified: fetchResult.LastModified,
                originUrl: fetchResult.FeedUrl,
                lastStatus: fetchResult.Status);

            if (fetchResult.Feed != null)
            {
                var feed = fetchResult.Feed;
                var existingItems = new HashSet<string>(
                    from existingFeed in river.UpdatedFeeds.Feeds
                    from item in existingFeed.Items
                    where item.Id != null
                    select item.Id
                );
                var newItems = feed.Items.RemoveAll(item => existingItems.Contains(item.Id));
                if (newItems.Count > 0)
                {
                    feed = new RiverFeed(feed, items: newItems);
                    updatedFeeds = new UpdatedFeeds(
                        river.UpdatedFeeds,
                        feeds: river.UpdatedFeeds.Feeds.Insert(0, feed));
                }
            }

            return new River(
                river,
                updatedFeeds: updatedFeeds,
                metadata: metadata);
        }

        public static async Task<River> FetchAndUpdateRiver(Uri uri, CancellationToken cancellationToken)
        {
            River river = await RiverFeedStore.LoadRiverForFeed(uri);
            if ((river.Metadata.LastStatus != HttpStatusCode.MovedPermanently) &&
                (river.Metadata.LastStatus != HttpStatusCode.Gone))
            {
                river = await UpdateRiver(river, cancellationToken);
                await RiverFeedStore.WriteRiver(uri, river);
            }

            if (river.Metadata.LastStatus == HttpStatusCode.MovedPermanently)
            {
                return await FetchAndUpdateRiver(river.Metadata.OriginUrl, cancellationToken);
            }

            return river;
        }

        static void Main(string[] args)
        {
            try
            {
                // Trace.Listeners.Add(new ConsoleTraceListener());

                XDocument doc = XDocument.Load(@"C:\Users\John\Downloads\NewsBlur-DeCarabas-2016-11-08");
                XElement body = doc.Root.Element(XNames.OPML.Body);

                List<OpmlEntry> feeds =
                    (from elem in body.Descendants(XNames.OPML.Outline)
                     where elem.Attribute(XNames.OPML.XmlUrl) != null
                     select OpmlEntry.FromXml(elem)).ToList();

                //foreach (var feed in feeds)
                //{
                //    //if (feed.XmlUrl.Host != "totalpartydeath.typepad.com") { continue; }
                //    Console.WriteLine("Working on {0} next...", feed.XmlUrl);
                //    Console.ReadLine();
                //    FetchAndUpdateRiver(feed.XmlUrl, CancellationToken.None).Wait();
                //    Console.WriteLine("Done.");
                //    Console.ReadLine();
                //}

                Stopwatch loadTimer = Stopwatch.StartNew();
                Console.WriteLine("Starting {0} feeds...", feeds.Count);
                var parses =
                    (from entry in feeds
                     select new
                     {
                         url = entry.XmlUrl,
                         task = FetchAndUpdateRiver(entry.XmlUrl, CancellationToken.None),
                     }).ToList();
                Task.WaitAll(parses.Select(p => p.task).ToArray());
                Console.WriteLine("Refreshed {0} feeds in {1}", feeds.Count, loadTimer.Elapsed);

                //Uri uri = new Uri("http://davepeck.org/feed/");
                //var parses = new[] { new { url = uri, task = FetchAndUpdateRiver(uri, CancellationToken.None) } };



                //foreach (var parse in parses.ToList())
                //{
                //    parse.task.Wait();

                //    //Console.WriteLine(parse.url);
                //    //foreach (RiverFeed feed in parse.task.Result.UpdatedFeeds.Feeds)
                //    //{
                //    //    DumpFeed(feed);
                //    //}
                //    //Console.ReadLine();
                //}
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }

        private static void DumpFeed(RiverFeed riverFeed)
        {
            if (riverFeed != null)
            {
                Console.WriteLine("{0}", riverFeed.FeedTitle);
                Console.WriteLine(new String('=', riverFeed.FeedTitle.Length));
                Console.WriteLine(riverFeed.FeedDescription);
                Console.WriteLine();

                foreach (RiverItem item in riverFeed.Items)
                {
                    Console.WriteLine(item.Title);
                    Console.WriteLine(new String('-', item.Title.Length));
                    Console.WriteLine(item.Body);
                    Console.WriteLine();
                }
            }
        }
    }
}
