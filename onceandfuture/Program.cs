using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using Newtonsoft.Json;
using Polly;
using Serilog;
using Serilog.Events;

// TODO: Save/load to blob stores
// TODO: Post titles can still be way too long. (cap to say 64?)

namespace onceandfuture
{
    static class Log
    {
        readonly static ConcurrentDictionary<string, ILogger> TaggedLoggers =
            new ConcurrentDictionary<string, ILogger>();
        readonly static Func<string, ILogger> logCreator = Create;

        static ILogger Create(string tag) => Serilog.Log.Logger.ForContext("tag", tag);
        static ILogger Get([CallerMemberName]string tag = null) => TaggedLoggers.GetOrAdd(tag, logCreator);

        public static void BadDate(string url, string date)
        {
            Get().Warning("{url}: Unparsable date encountered: {date}", url, date);
        }

        public static void NetworkError(Uri uri, HttpRequestException requestException, Stopwatch loadTimer)
        {
            Get().Error(requestException, "{uri}: {elapsed} ms: Network Error", uri, loadTimer.ElapsedMilliseconds);
        }

        public static void XmlError(Uri uri, XmlException xmlException, Stopwatch loadTimer)
        {
            Get().Error(xmlException, "{uri}: {elapsed} ms: XML Error", uri, loadTimer.ElapsedMilliseconds);
        }

        public static void BeginGetFeed(Uri uri)
        {
            Get().Information("{uri}: Begin fetching feed", uri);
        }

        public static void EndGetFeed(
            Uri uri,
            string version,
            HttpResponseMessage response,
            RiverFeed result,
            Stopwatch loadTimer
        )
        {
            Get().Information(
                "{uri}: Fetched {item_count} items from {feed} feed in {elapsed} ms",
                uri,
                result.Items.Count,
                version,
                loadTimer.ElapsedMilliseconds
            );
        }

        public static void UnrecognizableFeed(Uri uri, HttpResponseMessage response, string body, Stopwatch loadTimer)
        {
            Get().Error(
                "{uri}: Could not identify feed type in {elapsed} ms: {body}",
                uri,
                loadTimer.ElapsedMilliseconds,
                body
            );
        }

        public static void EndGetFeedFailure(Uri uri, HttpResponseMessage response, string body, Stopwatch loadTimer)
        {
            Get().Error(
                "{uri}: Got failure status code {code} in {elapsed} ms: {body}",
                uri,
                response.StatusCode,
                loadTimer.ElapsedMilliseconds,
                body
            );
        }

        public static void EndGetFeedNotModified(Uri uri, HttpResponseMessage response, Stopwatch loadTimer)
        {
            Get().Information("{uri}: Got feed not modified in {elapsed} ms", uri, loadTimer.ElapsedMilliseconds);
        }

        public static void EndGetFeedMovedPermanently(Uri uri, HttpResponseMessage response, Stopwatch loadTimer)
        {
            Get().Information(
                "{uri}: Feed moved permanently to {location} in {elapsed} ms",
                uri,
                response.Headers.Location,
                loadTimer.ElapsedMilliseconds);
        }

        public static void ConsideringImage(Uri baseUrl, Uri uri, string kind, int area, float ratio)
        {
            Get().Information(
                "{baseUrl}: Considering image {url} ({kind}) (area: {area}, ratio: {ratio})",
                baseUrl, uri, kind, area, ratio);
        }

        public static void NewBestImage(Uri baseUrl, Uri uri, string kind, int area, float ratio)
        {
            Get().Information(
                "{baseurl}: New best image: {url} ({kind}) (area: {area}, ratio: {ratio})",
                baseUrl, uri, kind, area, ratio);
        }

        public static void ThumbnailErrorResponse(Uri baseUrl, Uri imageUri, string kind, HttpResponseMessage response)
        {
            Get().Error(
                "{baseUrl}: {url} ({kind}): Error From Host: {status} {reason}",
                baseUrl, imageUri, kind, response.StatusCode, response.ReasonPhrase);
        }

        public static void InvalidThumbnailImageFormat(Uri baseUrl, Uri imageUri, string kind, ArgumentException ae)
        {
            // N.B.: Logging the ArgumentException is pointless.
            Get().Warning("{baseUrl}: {url} ({kind}): Is not a valid image", baseUrl, imageUri, kind);
        }

        public static void ThumbnailNetworkError(Uri baseUrl, Uri imageUri, string kind, HttpRequestException hre)
        {
            Get().Error(hre, "{baseUrl}: {url} ({kind}): Network Error", baseUrl, imageUri, kind);
        }

        public static void FoundThumbnail(Uri baseUrl, Uri uri, string kind)
        {
            Get().Information("{baseUrl}: Found thumbnail {url} ({kind})", baseUrl, uri, kind);
        }

        public static void BeginLoadThumbnails(Uri baseUri)
        {
            Get().Information("{baseUrl}: Loading thumbnails...", baseUri);
        }

        public static void EndLoadThumbnails(Uri baseUri, RiverItem[] items, Stopwatch loadTimer)
        {
            Get().Information(
                "{baseUrl}: Finished loading thumbs for {count} items in {elapsed} ms",
                baseUri,
                items.Length,
                loadTimer.ElapsedMilliseconds);
        }

        public static void NoThumbnailFound(Uri baseUrl)
        {
            Get().Information("{baseUrl}: No suitable thumbnails found.", baseUrl);
        }

        public static void EndGetThumbsFromSoup(Uri baseUrl, int length, Stopwatch loadTimer)
        {
            Get().Information(
                "{baseUrl}: Loaded {length} thumbnails in {elapsed} ms",
                baseUrl, length, loadTimer.ElapsedMilliseconds);
        }

        public static void BeginGetThumbsFromSoup(Uri baseUrl, int length)
        {
            Get().Information("{baseUrl}: Checking {length} thumbnails...", baseUrl, length);
        }

        public static void ThumbnailTooSmall(Uri baseUrl, Uri uri, string kind, int area)
        {
            Get().Information("{baseUrl}: {url} ({kind}): Too small ({area})", baseUrl, uri, kind, area);
        }

        public static void ThumbnailTooOblong(Uri baseUrl, Uri uri, string kind, float ratio)
        {
            Get().Information("{baseUrl}: {url} ({kind}): Too oblong ({ratio})", baseUrl, uri, kind, ratio);
        }

        public static void ThumbnailSuccessCacheHit(Uri baseUrl, Uri imageUrl)
        {
            Get().Information("{baseUrl}: {url}: Cached Success", baseUrl, imageUrl);
        }

        public static void ThumbnailErrorCacheHit(Uri baseUrl, Uri imageUrl, object cachedObject)
        {
            Get().Information("{baseUrl}: {url}: Cached Error: {error}", baseUrl, imageUrl, (string)cachedObject);
        }

        public static void FeedTimeout(Uri uri, Stopwatch loadTimer)
        {
            Get().Error("{url}: Timeout after {elapsed} ms", uri, loadTimer.ElapsedMilliseconds);
        }

        public static void ThumbnailTimeout(Uri baseUrl, Uri imageUri, string kind)
        {
            Get().Error("{baseUrl}: {url} ({kind}): Timeout", baseUrl, imageUri, kind);
        }

        public static void FindThumbnailNetworkError(Uri uri, HttpRequestException hre)
        {
            Get().Error(hre, "{url}: Network Error", uri);
        }

        public static void FindThumbnailTimeout(Uri uri)
        {
            Get().Error("{url}: Timeout", uri);
        }

        public static void FindThumbnailServerError(Uri uri, HttpResponseMessage response)
        {
            Get().Error("{url}: Server error: {code} {reason}", uri, response.StatusCode, response.ReasonPhrase);
        }

        public static void HttpRetry(
            Exception exception, TimeSpan timespan, int retryCount, Context context)
        {
            object url;
            context.TryGetValue("uri", out url);
            Get().Warning(
                exception, "HTTP error detected from {url}, retry {retry} after {ts}", url, retryCount, timespan);
        }

        public static void PutObjectComplete(string bucket, string name, string type, Stopwatch timer)
        {
            Get().Verbose(
                "Put Object: {bucket}/{name} ({type}) in {elapsed}ms",
                bucket, name, type, timer.ElapsedMilliseconds
            );
        }

        public static void GetObjectComplete(string bucket, string name, Stopwatch timer)
        {
            Get().Verbose(
                "Get Object: {bucket}/{name} in {elapsed}ms",
                bucket, name, timer.ElapsedMilliseconds
            );
        }

        public static void PutObjectError(
            string bucket, string name, string type, AmazonS3Exception error, Stopwatch timer)
        {
            Get().Error(
                error,
                "Put Object: ERROR {bucket}/{name} ({type}) in {elapsed}ms: {code}: {body}",
                bucket, name, type, timer.ElapsedMilliseconds, error.ErrorCode, error.ResponseBody
            );
        }

        public static void GetObjectError(string bucket, string name, AmazonS3Exception error, Stopwatch timer)
        {
            Get().Error(
                error,
                "Get Object: ERROR {bucket}/{name} in {elapsed}ms: {code}: {body}",
                bucket, name, timer.ElapsedMilliseconds, error.ErrorCode, error.ResponseBody
            );
        }

        public static void GetObjectNotFound(string bucket, string name, Stopwatch timer)
        {
            Get().Information(
                "Object {name} not found in S3 bucket {bucket} ({elapsed}ms)",
                name, bucket, timer.ElapsedMilliseconds
            );
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

        public static Uri ParseLink(string value, XElement xe)
        {
            if (value == null) { return null; }

            Uri result;
            if (!Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out result)) { return null; }
            if (!result.IsAbsoluteUri)
            {
                Uri baseUri;
                if (!String.IsNullOrWhiteSpace(xe.BaseUri) && Uri.TryCreate(xe.BaseUri, UriKind.Absolute, out baseUri))
                {
                    Uri absUri;
                    if (Uri.TryCreate(baseUri, result, out absUri)) { result = absUri; }
                }
            }
            return result;
        }

        public static Uri Rebase(Uri link, Uri baseUri)
        {
            if (link == null) { return null; }
            if (link.IsAbsoluteUri) { return link; }
            if (baseUri == null) { return link; }

            Uri absUri;
            if (!Uri.TryCreate(baseUri, link, out absUri)) { return link; }
            return baseUri;
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

    class BlobStore
    {
        readonly string bucket;
        readonly AmazonS3Client client;

        // In deployment, use this?
        // Credentials stored in the AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables.
        public BlobStore(string bucket)
        {
            this.bucket = bucket;
            this.client = new AmazonS3Client(region: RegionEndpoint.USWest2);
        }

        public Uri GetObjectUri(string name)
        {
            return new Uri("https://s3-us-west-2.amazonaws.com/" + this.bucket + "/" + Uri.EscapeDataString(name));
        }

        public async Task<byte[]> GetObject(string name)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                using (GetObjectResponse response = await this.client.GetObjectAsync(this.bucket, name))
                {
                    byte[] data = new byte[response.ResponseStream.Length];
                    int cursor = 0;
                    while (cursor != data.Length)
                    {
                        int read = await response.ResponseStream.ReadAsync(data, cursor, data.Length - cursor);
                        if (read == 0) { break; }
                        cursor += read;
                    }
                    Log.GetObjectComplete(this.bucket, name, timer);
                    return data;
                }
            }
            catch (Amazon.S3.AmazonS3Exception s3e)
            {
                if (s3e.ErrorCode == "NoSuchKey")
                {
                    Log.GetObjectNotFound(this.bucket, name, timer);
                    return null;
                }
                Log.GetObjectError(this.bucket, name, s3e, timer);
                throw;
            }
        }

        public async Task PutObject(string name, string type, Stream stream)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                await this.client.PutObjectAsync(new PutObjectRequest
                {
                    AutoCloseStream = false,
                    BucketName = this.bucket,
                    Key = name,
                    ContentType = type,
                    InputStream = stream,
                });
                Log.PutObjectComplete(this.bucket, name, type, timer);
            }
            catch (AmazonS3Exception e)
            {
                Log.PutObjectError(this.bucket, name, type, e, timer);
                throw;
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
            public static readonly XName DateCreated = XName.Get("dateCreated");
            public static readonly XName DateModified = XName.Get("dateModified");
            public static readonly XName Opml = XName.Get("opml");
            public static readonly XName Outline = XName.Get("outline");
            public static readonly XName Head = XName.Get("head");
            public static readonly XName HtmlUrl = XName.Get("htmlUrl");
            public static readonly XName Text = XName.Get("text");
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

    public class RiverItem
    {
        [JsonConstructor]
        public RiverItem(
            RiverItem existingItem = null,
            string title = null,
            Uri link = null,
            string body = null,
            DateTime? pubDate = null,
            Uri permaLink = null,
            string comments = null,
            string id = null,
            RiverItemThumbnail thumbnail = null,
            IEnumerable<RiverItemEnclosure> enclosures = null)
        {
            Title = title ?? existingItem?.Title ?? String.Empty;
            Link = link ?? existingItem?.Link;
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
        public Uri Link { get; }

        [JsonProperty(PropertyName = "body")]
        public string Body { get; }

        [JsonProperty(PropertyName = "pubDate")]
        public DateTime? PubDate { get; }

        [JsonProperty(PropertyName = "permaLink")]
        public Uri PermaLink { get; }

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
            Uri url = null,
            string type = null,
            string length = null)
        {
            Url = url ?? existingEnclosure?.Url;
            Type = type ?? existingEnclosure?.Type;
            Length = length ?? existingEnclosure?.Length;
        }

        [JsonProperty(PropertyName = "url")]
        public Uri Url { get; }

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

    class RiverFeedStore
    {
        readonly BlobStore blobStore = new BlobStore("onceandfuture");

        static string GetNameForUri(Uri feedUri)
        {
            return Util.HashString(feedUri.AbsoluteUri);
        }

        public async Task<River> LoadRiverForFeed(Uri feedUri)
        {
            byte[] blob = await this.blobStore.GetObject(GetNameForUri(feedUri));
            if (blob == null)
            {
                return new River(metadata: new RiverFeedMeta(originUrl: feedUri));
            }

            using (var memoryStream = new MemoryStream(blob))
            using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
            {
                string text = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<River>(text);
            }
        }

        public async Task WriteRiver(Uri uri, River river)
        {
            byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(river));
            using (var memoryStream = new MemoryStream(data))
            {
                await this.blobStore.PutObject(GetNameForUri(uri), "application/json", memoryStream);
            }
        }
    }

    class RiverThumbnailStore
    {
        readonly BlobStore blobStore = new BlobStore("onceandfuture-thumbs");

        public async Task<Uri> StoreImage(byte[] image)
        {
            MemoryStream stream = new MemoryStream();
            using (Image source = Image.FromStream(new MemoryStream(image)))
            {
                source.Save(stream, ImageFormat.Png);
            }

            stream.Position = 0;
            byte[] hash = SHA1.Create().ComputeHash(stream);
            string fileName = Convert.ToBase64String(hash).Replace('/', '-') + ".png";

            await this.blobStore.PutObject(fileName, "image/png", stream);
            return this.blobStore.GetObjectUri(fileName);
        }
    }

    static class EntropyCropper
    {
        const int MaximumSlice = 10;

        static byte[] ToGreyscale(Bitmap image)
        {
            byte[] values = new byte[image.Width * image.Height];
            int[] lineBuffer = new int[image.Width];
            BitmapData data = image.LockBits(
                new Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb); // Just... easiest?
            try
            {
                // If stride is negative then we're bottom up.
                int targetIdx, lineSize;
                if (data.Stride < 0)
                {
                    targetIdx = (data.Height - 1) * data.Width;
                    lineSize = -data.Width;
                }
                else
                {
                    targetIdx = 0;
                    lineSize = data.Width;
                }

                int actualStride = Math.Abs(data.Stride);
                for (int y = 0; y < data.Height; y++)
                {
                    IntPtr scanLine = data.Scan0 + (y * actualStride);
                    Marshal.Copy(scanLine, lineBuffer, 0, lineBuffer.Length);

                    for (int x = 0; x < data.Width; x++)
                    {
                        // Extract RGB.
                        // AARRGGBB
                        int pix = lineBuffer[x];
                        var r = (byte)((0x00FF0000 & pix) >> 16);
                        var g = (byte)((0x0000FF00 & pix) >> 8);
                        var b = (byte)((0x000000FF & pix) >> 0);

                        // This is a terrible intensity function, but it works.
                        float i = (r + b + g) / (3.0f);

                        var ii = (int)i;
                        float ri = i - ii;
                        if (ri > 0.5f)
                        {
                            ii += 1;
                        }

                        values[targetIdx + x] = (byte)ii;
                    }
                    targetIdx += lineSize;
                }
            }
            finally
            {
                image.UnlockBits(data);
            }
            return values;
        }

        static double Entropy(byte[] pix, int stride, int[] hist, int left, int top, int right, int bottom)
        {
            Array.Clear(hist, 0, hist.Length);
            for (int iy = top; iy < bottom; iy++)
            {
                int idx = (iy * stride) + left;
                for (int ix = left; ix < right; ix++)
                {
                    hist[pix[idx]]++;
                    idx++;
                }
            }

            double sum = 0;

            // In the math this is sum(hist), but that's weird because it's really just the area of the bitmap.
            double area = (right - left) * (bottom - top);
            for (int i = 0; i < hist.Length; i++)
            {
                if (hist[i] != 0)
                {
                    double v = ((double)hist[i]) / area;
                    sum += v * Math.Log(v, 2.0);
                }
            }
            return -sum;
        }

        static void CropVertical(byte[] pix, int width, int height, int targetHeight, out int top, out int bottom)
        {
            int[] hist = new int[256];
            top = 0;
            bottom = height;
            while (bottom - top > targetHeight)
            {
                int sliceHeight = Math.Min((bottom - top) - targetHeight, MaximumSlice);

                double topEntropy = Entropy(
                    pix, width, hist,
                    0, top,
                    width, top + sliceHeight);

                double bottomEntropy = Entropy(
                    pix, width, hist,
                    0, bottom - sliceHeight,
                    width, bottom);
                if (topEntropy < bottomEntropy)
                {
                    // Top has less entropy, cut it by moving top down.
                    top += sliceHeight;
                }
                else
                {
                    // Bottom has less entropy, cut it by moving bottom up.
                    bottom -= sliceHeight;
                }
            }
        }

        static void CropHorizontal(byte[] pix, int width, int height, int targetWidth, out int left, out int right)
        {
            int[] hist = new int[256];
            left = 0;
            right = width;
            while (right - left > targetWidth)
            {
                int sliceWidth = Math.Min((right - left) - targetWidth, MaximumSlice);

                double leftEntropy = Entropy(
                    pix, width, hist,
                    left, 0,
                    left + sliceWidth, height);

                double rightEntropy = Entropy(
                    pix, width, hist,
                    right - sliceWidth, 0,
                    right, height);
                if (leftEntropy < rightEntropy)
                {
                    // Left has less entropy, cut it by moving left.
                    left += sliceWidth;
                }
                else
                {
                    // Right has less entropy, cut it by moving right.
                    right -= sliceWidth;
                }
            }
        }

        static void CropSquare(
            byte[] pix, int width, int height, out int left, out int top, out int right, out int bottom)
        {
            if (width > height)
            {
                top = 0; bottom = height;
                CropHorizontal(pix, width, height, height, out left, out right);
            }
            else
            {
                left = 0; right = width;
                CropVertical(pix, width, height, width, out top, out bottom);
            }
        }

        public static Bitmap Crop(Bitmap image, int targetSize)
        {
            byte[] values = ToGreyscale(image);
            int width = image.Width;
            int height = image.Height;

            int left, right, top, bottom;
            CropSquare(values, width, height, out left, out top, out right, out bottom);

            var destPixelFormat = image.PixelFormat;
            if ((destPixelFormat & PixelFormat.Indexed) != 0) { destPixelFormat = PixelFormat.Format32bppArgb; }

            var destRect = new Rectangle(0, 0, targetSize, targetSize);
            var destImage = new Bitmap(targetSize, targetSize, destPixelFormat);

            // StackOverflow suggests this but this doesn't work on Mono so I ain't doing it.
            // destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(
                        image,
                        destRect,
                        left, top, right - left, bottom - top,
                        GraphicsUnit.Pixel,
                        wrapMode);
                }
            }
            return destImage;
        }
    }

    static class Policies
    {
        static Random random = new Random();

        public static readonly ContextualPolicy HttpPolicy = Policy
            .Handle<HttpRequestException>(ValidateHttpRequestException)
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: ExponentialRetryTimeWithJitter,
                onRetry: (exc, ts, cnt, ctxt) => Log.HttpRetry(exc, ts, cnt, ctxt));

        static bool ValidateHttpRequestException(HttpRequestException hre)
        {
            var iwe = hre.InnerException as WebException;
            if (iwe != null)
            {
                if (iwe.Message.Contains("The remote name could not be resolved")) { return false; }
                if (iwe.Message.Contains("The server committed a protocol violation")) { return false; }
            }

            return true;
        }

        static TimeSpan ExponentialRetryTimeWithJitter(int retryAttempt)
        {
            var baseTime = TimeSpan.FromSeconds(Math.Pow(3, retryAttempt));

            int jitterInterval = (int)(baseTime.TotalMilliseconds / 2.0);
            var jitter = TimeSpan.FromMilliseconds(random.Next(-jitterInterval, jitterInterval));

            return baseTime + jitter;
        }
    }

    class ThumbnailExtractor
    {
        readonly RiverThumbnailStore thumbnailStore = new RiverThumbnailStore();

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

        public async Task<RiverItem[]> LoadItemThumbnailsAsync(
            Uri baseUri, RiverItem[] items, CancellationToken token)
        {
            Stopwatch loadTimer = Stopwatch.StartNew();
            Log.BeginLoadThumbnails(baseUri);
            Task<RiverItem>[] itemTasks = new Task<RiverItem>[items.Length];
            for (int i = 0; i < itemTasks.Length; i++)
            {
                itemTasks[i] = GetItemThumbnailAsync(baseUri, items[i], token);
            }

            RiverItem[] newItems = await Task.WhenAll(itemTasks);
            Log.EndLoadThumbnails(baseUri, newItems, loadTimer);
            return newItems;
        }

        async Task<RiverItem> GetItemThumbnailAsync(Uri baseUri, RiverItem item, CancellationToken token)
        {
            if (item.Thumbnail != null) { return item; }
            if (item.Link == null) { return item; }

            Uri itemLink = Util.Rebase(item.Link, baseUri);
            ImageData sourceImage = await FindThumbnailAsync(itemLink, token);
            if (sourceImage == null) { return item; }
            ImageData thumbnail = MakeThumbnail(sourceImage);

            Uri thumbnailUri = await this.thumbnailStore.StoreImage(thumbnail.Data);
            return new RiverItem(
                item,
                thumbnail: new RiverItemThumbnail(
                    url: thumbnailUri.AbsoluteUri,
                    width: thumbnail.Width,
                    height: thumbnail.Height
                )
            );
        }

        static ImageData MakeThumbnail(ImageData sourceImage)
        {
            using (var ss = new MemoryStream(sourceImage.Data))
            using (var src = (Bitmap)Image.FromStream(ss))
            using (var dst = EntropyCropper.Crop(src, 400))
            using (var ds = new MemoryStream())
            {
                dst.Save(ds, ImageFormat.Png);
                ds.Position = 0;
                byte[] buffer = new byte[ds.Length];
                ds.Read(buffer, 0, buffer.Length);
                return new ImageData(dst.Width, dst.Height, buffer);
            }
        }

        static async Task<ImageData> FindThumbnailAsync(Uri uri, CancellationToken cancellationToken)
        {
            try
            {
                HttpResponseMessage response = await Policies.HttpPolicy.ExecuteAsync(
                    (ct) => client.GetAsync(uri, ct),
                    new Dictionary<string, object> { { "uri", uri } },
                    cancellationToken);
                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.FindThumbnailServerError(uri, response);
                        return null;
                    }

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
            catch (TaskCanceledException)
            {
                Log.FindThumbnailTimeout(uri);
            }
            catch (HttpRequestException hre)
            {
                Log.FindThumbnailNetworkError(uri, hre);
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
                    if (bestImageUrl != null) { CacheError(bestImageUrl, "Not the best"); }
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
                // N.B.: We put the whole bit of cache logic in here because somebody might succeed or fail altogether
                //       while we wait on retries.
                return await Policies.HttpPolicy.ExecuteAsync(async (ct) =>
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

                        HttpResponseMessage response = await client.SendAsync(request, ct);

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

                    },
                    new Dictionary<string, object> { { "uri", imageUrl.Uri } },
                    cancellationToken);
            }
            catch (TaskCanceledException tce)
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

        class ImageUrl
        {
            public string Kind;
            public Uri Uri;
        }
    }

    class RiverFeedParser
    {
        readonly ThumbnailExtractor thumbnailExtractor = new ThumbnailExtractor();
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
                { XNames.RSS.Link,        (ri, xe) => new RiverItem(ri, link: Util.ParseLink(xe.Value, xe)) },
                { XNames.RSS.Description, (ri, xe) => new RiverItem(ri, body: Util.ParseBody(xe)) },
                { XNames.RSS.Comments,    (ri, xe) => new RiverItem(ri, comments: xe.Value) },
                { XNames.RSS.PubDate,     (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.RSS.Guid,        (ri, xe) => HandleGuid(ri, xe) },
                { XNames.RSS.Enclosure,   (ri, xe) => HandleEnclosure(ri, xe) },

                { XNames.RSS10.Title,       (ri, xe) => new RiverItem(ri, title: Util.ParseBody(xe)) },
                { XNames.RSS10.Link,        (ri, xe) => new RiverItem(ri, link: Util.ParseLink(xe.Value, xe)) },
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
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;

            client = new HttpClient(httpClientHandler, false);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TheOnceAndFuture/1.0");
        }

        public async Task<River> FetchAndUpdateRiver(
            RiverFeedStore feedStore,
            Uri uri,
            CancellationToken cancellationToken)
        {
            River river = await feedStore.LoadRiverForFeed(uri);
            if ((river.Metadata.LastStatus != HttpStatusCode.MovedPermanently) &&
                (river.Metadata.LastStatus != HttpStatusCode.Gone))
            {
                river = await UpdateAsync(river, cancellationToken);
                await feedStore.WriteRiver(uri, river);
            }

            if (river.Metadata.LastStatus == HttpStatusCode.MovedPermanently)
            {
                return await FetchAndUpdateRiver(feedStore, river.Metadata.OriginUrl, cancellationToken);
            }

            return river;
        }

        public async Task<River> UpdateAsync(River river, CancellationToken cancellationToken)
        {
            FetchResult fetchResult = await FetchAsync(
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
                RiverItem[] newItems = feed.Items.Where(item => !existingItems.Contains(item.Id)).ToArray();
                if (newItems.Length > 0)
                {
                    Uri baseUri;
                    if (String.IsNullOrWhiteSpace(feed.WebsiteUrl) ||
                        !Uri.TryCreate(feed.WebsiteUrl, UriKind.Absolute, out baseUri))
                    {
                        baseUri = feed.FeedUrl;
                    }
                    for (int i = 0; i < newItems.Length; i++)
                    {
                        newItems[i] = Rebase(newItems[i], baseUri);
                    }

                    newItems = await this.thumbnailExtractor.LoadItemThumbnailsAsync(
                        baseUri, newItems, cancellationToken);
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

        static RiverItem Rebase(RiverItem item, Uri baseUri)
        {
            return new RiverItem(
                item,
                link: Util.Rebase(item.Link, baseUri),
                permaLink: Util.Rebase(item.PermaLink, baseUri),
                enclosures: item.Enclosures.Select(
                    e => new RiverItemEnclosure(e, url: Util.Rebase(e.Url, baseUri))
                )
            );
        }

        static async Task<FetchResult> FetchAsync(
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
                    response = await Policies.HttpPolicy.ExecuteAsync((ct) =>
                        {
                            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                            if (etag != null) { request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag)); }
                            request.Headers.IfModifiedSince = lastModified;

                            return client.SendAsync(request, ct);
                        },
                        new Dictionary<string, object> { { "uri", requestUri } },
                        cancellationToken);

                    if ((response.StatusCode != HttpStatusCode.TemporaryRedirect) &&
                        (response.StatusCode != HttpStatusCode.Found) &&
                        (response.StatusCode != HttpStatusCode.SeeOther))
                    {
                        break;
                    }

                    requestUri = response.Headers.Location;
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
            string rel = link.Attribute(XNames.Atom.Rel)?.Value ?? "alternate";
            string type = link.Attribute(XNames.Atom.Type)?.Value ?? "text/html";
            string href = link.Attribute(XNames.Atom.Href)?.Value;

            if (String.Equals(rel, "alternate", StringComparison.OrdinalIgnoreCase) &&
                type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                feed = new RiverFeed(feed, websiteUrl: link.Attribute(XNames.Atom.Href)?.Value);
            }

            return feed;
        }

        static RiverItem HandleAtomLink(RiverItem item, XElement link)
        {
            string rel = link.Attribute(XNames.Atom.Rel)?.Value ?? "alternate";
            string type = link.Attribute(XNames.Atom.Type)?.Value ?? "text/html";
            string href = link.Attribute(XNames.Atom.Href)?.Value;

            if (String.Equals(rel, "alternate", StringComparison.OrdinalIgnoreCase) &&
                type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                item = new RiverItem(item, link: Util.ParseLink(href, link));
            }

            if (String.Equals(rel, "self", StringComparison.OrdinalIgnoreCase) &&
                type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                item = new RiverItem(item, permaLink: Util.ParseLink(href, link));
            }

            if (link.Attribute(XNames.Atom.Rel)?.Value == "enclosure")
            {
                item = new RiverItem(item, enclosures: item.Enclosures.Add(new RiverItemEnclosure(
                    length: link.Attribute(XNames.Atom.Length)?.Value,
                    type: type,
                    url: Util.ParseLink(href, link)
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
                    url: Util.ParseLink(element.Attribute(XNames.RSS.Url)?.Value, element)
                )));
        }

        static RiverItem HandleGuid(RiverItem item, XElement element)
        {
            item = new RiverItem(item, id: element.Value);

            if (item.Id.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                item.Id.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                item = new RiverItem(item, permaLink: Util.ParseLink(item.Id, element));
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
                else if (ri.PermaLink != null) { title = ri.PermaLink.AbsoluteUri; }
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
    }

    static class SubscriptionStore
    {
        public static async Task<XElement> GetSubscriptionsFor(string user)
        {
            try
            {
                using (TextReader reader = File.OpenText(user))
                {
                    string body = await reader.ReadToEndAsync();
                    XDocument doc = XDocument.Parse(body);
                    return doc.Root.Element(XNames.OPML.Body) ?? new XElement(XNames.OPML.Body);
                }
            }
            catch (FileNotFoundException)
            {
                return new XElement(XNames.OPML.Body);
            }
            throw new NotImplementedException();
        }

        public static Task SaveSubscriptionsFor(string user, XElement opmlBody)
        {
            XDocument doc = new XDocument(
                new XElement(
                    XNames.OPML.Opml,
                    new XAttribute(XNames.OPML.Version, "1.1"),
                    new XElement(
                        XNames.OPML.Head,
                        new XElement(XNames.OPML.Title, "Feeds for " + user),
                        new XElement(XNames.OPML.DateCreated, DateTimeOffset.UtcNow),
                        new XElement(XNames.OPML.DateModified, DateTimeOffset.UtcNow)),
                    opmlBody));
            using (XmlWriter writer = XmlWriter.Create(user))
            {
                doc.WriteTo(writer);
            }

            return Task.CompletedTask;
        }
    }


    class Program
    {


        // ---

        static readonly OptDef[] CommonOptions = new OptDef[]
        {
            new OptDef { Short='?', Long="help",    Help="Display this help." },
            new OptDef {
                Short ='v',
                Long ="verbose",
                Help ="Increase the verbosity level; specify more than once for more verbosity."
            },
        };

        static readonly Dictionary<string, OptDef[]> Verbs = new Dictionary<string, OptDef[]>
        {
            {
                "update", new[]
                {
                    new OptDef { Short='f', Long="feed", Help="The single feed URL to update.", Value=true },
                    new OptDef { Short='u', Long="user", Help="The user to update feeds for.", Value=true },
                }
            },
            {
                "show", new []
                {
                    new OptDef { Short='f', Long="feed", Help="The single feed URL to show.", Value=true },
                }
            },
            {
                "sub", new[]
                {
                    new OptDef { Short='u', Long="user",  Help="The user to add a subscription for", Value=true, Required=true },
                    new OptDef { Short='r', Long="river", Help="The river to add a subscription to", Value=true, Default="main" },
                    new OptDef { Short='f', Long="feed",  Help="The feed to add a subscription for", Value=true, Required=true },
                }
            },
            {
                "list", new[]
                {
                    new OptDef { Short='u', Long="user",  Help="The user to list the subscriptions for", Value=true, Required=true },
                }
            }
        };

        static int DoShow(ParsedOpts args)
        {
            List<OpmlEntry> feeds;
            if (args["feed"].Value != null)
            {
                Uri feedUrl;
                if (!Uri.TryCreate(args["feed"].Value, UriKind.Absolute, out feedUrl))
                {
                    Console.Error.WriteLine("Feed not a valid url: {0}", args["feed"].Value);
                    return 100;
                }
                feeds = new List<OpmlEntry> { new OpmlEntry(xmlUrl: feedUrl) };
            }
            else
            {
                XDocument doc = XDocument.Load(@"C:\Users\John\Downloads\NewsBlur-DeCarabas-2016-11-08");
                XElement body = doc.Root.Element(XNames.OPML.Body);

                feeds =
                    (from elem in body.Descendants(XNames.OPML.Outline)
                     where elem.Attribute(XNames.OPML.XmlUrl) != null
                     select OpmlEntry.FromXml(elem)).ToList();
            }

            var feedStore = new RiverFeedStore();
            foreach (var entry in feeds)
            {
                River river = feedStore.LoadRiverForFeed(entry.XmlUrl).Result;
                if (river.UpdatedFeeds.Feeds.Count > 0)
                {
                    foreach (RiverFeed feed in river.UpdatedFeeds.Feeds)
                    {
                        DumpFeed(feed);
                    }
                }
                else
                {
                    Console.WriteLine("No data for {0}", entry.XmlUrl);
                }

                Console.WriteLine("(Press enter to continue)");
                Console.ReadLine();
            }
            return 0;
        }

        static int DoUpdate(ParsedOpts args)
        {
            List<OpmlEntry> feeds;
            if (args["feed"].Value != null)
            {
                Uri feedUrl;
                if (!Uri.TryCreate(args["feed"].Value, UriKind.Absolute, out feedUrl))
                {
                    Console.Error.WriteLine("Feed not a valid url: {0}", args["feed"].Value);
                    return 100;
                }
                feeds = new List<OpmlEntry> { new OpmlEntry(xmlUrl: feedUrl) };
            }
            else if (args["user"].Value != null)
            {
                XElement body = SubscriptionStore.GetSubscriptionsFor(args["user"].Value).Result;

                feeds =
                    (from elem in body.Descendants(XNames.OPML.Outline)
                     where elem.Attribute(XNames.OPML.XmlUrl) != null
                     select OpmlEntry.FromXml(elem)).ToList();
            }
            else
            {
                Console.Error.WriteLine("Must specify either user or feed.");
                return -1;
            }

            Stopwatch loadTimer = Stopwatch.StartNew();

            var parser = new RiverFeedParser();
            var feedStore = new RiverFeedStore();
            var parses =
                (from entry in feeds
                 select new
                 {
                     url = entry.XmlUrl,
                     task = parser.FetchAndUpdateRiver(feedStore, entry.XmlUrl, CancellationToken.None),
                 }).ToList();

            Console.WriteLine("Started {0} feeds...", parses.Count);
            Task<River[]> doneTask = Task.WhenAll(parses.Select(p => p.task).ToArray()).ContinueWith(t =>
            {
                loadTimer.Stop();
                return t.Result;
            });

            doneTask.Wait();
            Console.WriteLine("Refreshed {0} feeds in {1}", parses.Count, loadTimer.Elapsed);
            return 0;
        }

        static int DoSubscribe(ParsedOpts args)
        {
            string user = args["user"].Value;
            string feed = args["feed"].Value;
            string riverName = args["river"].Value;

            // Check feed.
            var parser = new RiverFeedParser();
            var feedStore = new RiverFeedStore();
            River feedRiver = parser.FetchAndUpdateRiver(feedStore, new Uri(feed), CancellationToken.None).Result;
            if (feedRiver.Metadata.LastStatus < (HttpStatusCode)200 ||
                feedRiver.Metadata.LastStatus >= (HttpStatusCode)400)
            {
                Console.Error.WriteLine("Could not fetch feed {0}", feed);
                return -1;
            }


            string text = feed;
            string htmlUrl = feed;
            string xmlUrl = feedRiver.Metadata.OriginUrl.AbsoluteUri;
            if (feedRiver.UpdatedFeeds.Feeds.Count > 0)
            {
                RiverFeed rf = feedRiver.UpdatedFeeds.Feeds[0];
                text = rf.FeedTitle;
                htmlUrl = rf.WebsiteUrl;
            }

            XElement feedElement = new XElement(
                XNames.OPML.Outline,
                new XAttribute(XNames.OPML.HtmlUrl, htmlUrl),
                new XAttribute(XNames.OPML.Type, "rss"),
                new XAttribute(XNames.OPML.Version, "RSS"),
                new XAttribute(XNames.OPML.Title, text),
                new XAttribute(XNames.OPML.Text, text),
                new XAttribute(XNames.OPML.XmlUrl, xmlUrl));

            XElement opmlBody = SubscriptionStore.GetSubscriptionsFor(user).Result;

            // Find the river element. 
            XElement riverElement;
            if (String.Equals("main", riverName, StringComparison.OrdinalIgnoreCase))
            {
                riverElement = opmlBody;
            }
            else
            {
                riverElement =
                    (from element in opmlBody.Elements(XNames.OPML.Outline)
                     where element.Attribute(XNames.OPML.Text)?.Value == riverName
                     where element.Attribute(XNames.OPML.XmlUrl) == null
                     select element).FirstOrDefault();
            }

            if (riverElement == null)
            {
                riverElement = new XElement(
                    XNames.OPML.Outline,
                    new XAttribute(XNames.OPML.Title, riverName),
                    new XAttribute(XNames.OPML.Text, riverName));
                opmlBody.Add(riverElement);
            }

            XElement existingFeedElement =
                (from element in riverElement.Elements(XNames.OPML.Outline)
                 where element.Attribute(XNames.OPML.XmlUrl)?.Value == xmlUrl
                 select element).FirstOrDefault();
            if (existingFeedElement != null)
            {
                existingFeedElement.ReplaceWith(feedElement);
            }
            else
            {
                riverElement.Add(feedElement);
            }

            SubscriptionStore.SaveSubscriptionsFor(user, opmlBody).Wait();
            return 0;
        }

        static int DoList(ParsedOpts args)
        {
            string user = args["user"].Value;
            XElement opmlBody = SubscriptionStore.GetSubscriptionsFor(user).Result;
            Console.WriteLine(opmlBody.ToString());
            return 0;
        }

        static int Main(string[] args)
        {
            try
            {
                ParsedOpts parsedArgs = ParseOptions(args, CommonOptions, Verbs);
                if (parsedArgs.Error != null)
                {
                    Console.Error.WriteLine(parsedArgs.Error);
                    PrintHelp(Console.Error, CommonOptions, Verbs);
                    return 1;
                }
                if (parsedArgs["help"].Flag)
                {
                    PrintHelp(Console.Out, CommonOptions, Verbs);
                    return 0;
                }

                var logLevel = (LogEventLevel)Math.Max((int)(LogEventLevel.Error - parsedArgs["verbose"].Count), 0);
                Serilog.Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Is(logLevel)
                    .WriteTo.LiterateConsole()
                    .CreateLogger();

                switch (parsedArgs.Verb)
                {
                case "update": return DoUpdate(parsedArgs);
                case "show": return DoShow(parsedArgs);
                case "sub": return DoSubscribe(parsedArgs);
                case "list": return DoList(parsedArgs);
                }

                throw new NotSupportedException();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 99;
            }
        }

        static void DumpFeed(RiverFeed riverFeed)
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
                    Console.WriteLine("ID:        {0}", item.Id);
                    Console.WriteLine("Link:      {0}", item.Link);
                    Console.WriteLine("Permalink: {0}", item.PermaLink);
                    Console.WriteLine("Thumbnail: {0}", item.Thumbnail?.Url);
                    Console.WriteLine();
                    Console.WriteLine(item.Body);
                    Console.WriteLine();
                    if (item.Enclosures.Count > 0)
                    {
                        Console.WriteLine("  Enclosures:");
                        foreach (RiverItemEnclosure e in item.Enclosures)
                        {
                            Console.WriteLine("    {0} ({1}): {2}", e.Type, e.Length, e.Url);
                        }
                        Console.WriteLine();
                    }
                }
            }
        }

        // Yeah command line.

        class ParsedOpts
        {
            public string Error;
            public string Verb;
            public Dictionary<string, Opt> Opts { get; } = new Dictionary<string, Opt>();

            public Opt this[string key] => this.Opts[key];
        }

        class OptDef
        {
            public char Short;
            public string Long;
            public string Help;
            public bool Value;
            public string Default;
            public bool Required;
        }

        class Opt
        {
            public string Value;
            public int Count;

            public Opt(OptDef optDef)
            {
                this.Option = optDef;
                this.Value = optDef.Default;
            }

            public bool Flag => Count > 0;
            public OptDef Option { get; }
        }

        static ParsedOpts ParseOptions(string[] args, OptDef[] commonOptions, Dictionary<string, OptDef[]> verbs)
        {
            ParsedOpts results = new ParsedOpts();
            for (int i = 0; i < commonOptions.Length; i++)
            {
                results.Opts[commonOptions[i].Long] = new Opt(commonOptions[i]);
            }

            OptDef[] verbOptions = null;
            for (int argIndex = 0; argIndex < args.Length; argIndex++)
            {
                OptDef opt;
                string arg = args[argIndex];
                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    string[] parts = arg.Substring(2).Split(new char[] { '=' }, 2);
                    string longArg = parts[0];

                    string val = null;
                    if (parts.Length == 2) { val = parts[1]; }

                    opt = Array.Find(
                        commonOptions,
                        o => String.Equals(o.Long, longArg, StringComparison.OrdinalIgnoreCase));
                    if (opt == null && verbOptions != null)
                    {
                        opt = Array.Find(
                            verbOptions, o => String.Equals(o.Long, longArg, StringComparison.OrdinalIgnoreCase));
                    }

                    HandleOpt(results, arg, opt, val);
                }
                else if (arg[0] == '-')
                {
                    if (arg.Length == 1)
                    {
                        results.Error = String.Format("Unrecognized argument: '{0}'", arg);
                    }
                    else
                    {
                        for (int flagIndex = 1; flagIndex < arg.Length; flagIndex++)
                        {
                            string val = null;
                            char flag = arg[flagIndex];
                            if (flagIndex + 1 < arg.Length && arg[flagIndex] == '=')
                            {
                                val = arg.Substring(flagIndex + 2);
                                flagIndex = arg.Length;
                            }

                            opt = Array.Find(commonOptions, o => o.Short == flag);
                            if (opt == null && verbOptions != null)
                            {
                                opt = Array.Find(verbOptions, o => o.Short == flag);
                            }

                            HandleOpt(results, flag.ToString(), opt, val);
                            if (results.Error != null) { break; }
                        }
                    }
                }
                else
                {
                    if (results.Verb == null && verbs != null)
                    {
                        if (!verbs.TryGetValue(arg, out verbOptions))
                        {
                            results.Error = String.Format("Unknown verb: {0}", arg);
                        }
                        else
                        {
                            for (int verbOptionIndex = 0; verbOptionIndex < verbOptions.Length; verbOptionIndex++)
                            {
                                results.Opts[verbOptions[verbOptionIndex].Long] =
                                    new Opt(verbOptions[verbOptionIndex]);
                            }
                            results.Verb = arg;
                        }
                    }
                    else
                    {
                        results.Error = String.Format("Unrecognized argument: '{0}'", arg);
                    }
                }

                if (results.Error != null) { break; }
            }

            // Check for missing verb
            if (results.Error == null && results.Verb == null && verbs != null)
            {
                results.Error = String.Format(
                    "Did not find a verb; specify one of: {0}", String.Join(", ", verbs.Keys));
            }

            // Check for missing required option
            if (results.Error == null)
            {
                foreach (Opt opt in results.Opts.Values)
                {
                    if (opt.Option.Required && opt.Count == 0)
                    {
                        results.Error = String.Format("Missing required option {0}", opt.Option.Long);
                        break;
                    }
                }
            }

            return results;
        }

        static void HandleOpt(ParsedOpts results, string arg, OptDef opt, string value)
        {
            if (opt != null)
            {
                Opt optVal = results.Opts[opt.Long];
                if (value != null)
                {
                    if (!opt.Value)
                    {
                        results.Error = String.Format("Argument '{0}' doesn't take a value", arg);
                    }
                    else if (optVal.Count > 0)
                    {
                        results.Error = String.Format("Multiple values found for '{0}'", arg);
                    }
                    else
                    {
                        optVal.Value = value;
                    }
                }
                else if (opt.Value)
                {
                    results.Error = String.Format("Argument '{0}' requires a value", arg);
                }
                optVal.Count++;
            }
            else
            {
                results.Error = String.Format("Unrecognized argument: '{0}'", arg);
            }
        }

        static void PrintHelp(TextWriter output, OptDef[] commonOptions, Dictionary<string, OptDef[]> verbs)
        {
            output.WriteLine("TBD: Help");
        }
    }
}
