using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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

namespace onceandfuture
{
    static class Log
    {
        public static void BadDate(string url, string date)
        {
            Console.WriteLine("{0}: Unparsable date encountered: {1}", url, date);
        }

        public static void NetworkError(Uri uri, HttpRequestException requestException, Stopwatch loadTimer)
        {
            Console.WriteLine(
                "{0}: {2} ms: Network Error: {1}",
                uri,
                requestException.Message,
                loadTimer.ElapsedMilliseconds
            );
        }

        public static void XmlError(Uri uri, XmlException xmlException, Stopwatch loadTimer)
        {
            Console.WriteLine(
                "{0}: {2} ms: XML Error: {1}",
                uri,
                xmlException.Message,
                loadTimer.ElapsedMilliseconds
            );
        }

        public static void BeginGetFeed(Uri uri)
        {
            Console.WriteLine("{0}: Begin fetching", uri);
        }

        public static void EndGetFeed(
            Uri uri,
            string version,
            HttpResponseMessage response,
            RiverFeed result,
            Stopwatch loadTimer
        )
        {
            Console.WriteLine(
                "{0}: Fetched {1} items from {2} feed in {3} ms",
                uri,
                result.Items.Count,
                version,
                loadTimer.ElapsedMilliseconds
            );
        }

        public static void UnrecognizableFeed(Uri uri, HttpResponseMessage response, string body, Stopwatch loadTimer)
        {
            Console.WriteLine(
                "{0}: Could not identify feed type in {1} ms: {2}",
                uri,
                loadTimer.ElapsedMilliseconds,
                body
            );
        }

        public static void EndGetFeedFailure(Uri uri, HttpResponseMessage response, string body, Stopwatch loadTimer)
        {
            Console.WriteLine(
                "{0}: Got failure status code {1} in {2} ms: {3}",
                uri,
                response.StatusCode,
                loadTimer.ElapsedMilliseconds,
                body
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
            bool lastWasLine;

            HtmlFormatter() { }

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
                    foreach (INode child in node.ChildNodes)
                    {
                        if (!Visit(child)) { return false; }
                    }
                    if (node.NodeName == "P" || node.NodeName == "DIV" || node.NodeName == "BR")
                    {
                        if (!this.lastWasLine)
                        {
                            builder.AppendLine();
                            builder.AppendLine();
                            this.lastWasLine = true;
                        }
                    }
                    break;

                case NodeType.Text:
                case NodeType.CharacterData:
                case NodeType.EntityReference:
                    string[] parts = node.TextContent.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        builder.Append(parts[i]);
                        builder.Append(" ");
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
            string title = null,
            string text = null,
            string type = null,
            string version = null,
            Uri xmlUrl = null
        )
        {
            HtmlUrl = htmlUrl;
            Title = title ?? String.Empty;
            Text = text ?? String.Empty;
            Type = type ?? String.Empty;
            Version = version ?? String.Empty;
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

    class RiverFeed
    {
        readonly List<RiverItem> items = new List<RiverItem>();

        public string FeedTitle { get; set; } = String.Empty;
        public Uri FeedUrl { get; set; }
        public string WebsiteUrl { get; set; } = String.Empty;
        public string FeedDescription { get; set; } = String.Empty;
        public DateTime WhenLastUpdate { get; set; }
        public IList<RiverItem> Items => this.items;

        static readonly Dictionary<XName, Action<RiverFeed, XElement>> FeedElements =
            new Dictionary<XName, Action<RiverFeed, XElement>>
            {
                { XNames.RSS.Title,       (rf, xe) => rf.FeedTitle = xe.Value },
                { XNames.RSS10.Title,     (rf, xe) => rf.FeedTitle = xe.Value },
                { XNames.Atom.Title,      (rf, xe) => rf.FeedTitle = xe.Value },

                { XNames.RSS.Link,        (rf, xe) => rf.WebsiteUrl = xe.Value },
                { XNames.RSS10.Link,      (rf, xe) => rf.FeedTitle = xe.Value },
                { XNames.Atom.Link,       (rf, xe) => HandleAtomLink(rf, xe) },

                { XNames.RSS.Description,   (rf, xe) => rf.FeedDescription = Util.ParseBody(xe) },
                { XNames.RSS10.Description, (rf, xe) => rf.FeedTitle = xe.Value },

                { XNames.RSS.Item,        (rf, xe) => rf.Items.Add(RiverItem.LoadItem(xe)) },
                { XNames.RSS10.Item,      (rf, xe) => rf.Items.Add(RiverItem.LoadItem(xe)) },
                { XNames.Atom.Entry,      (rf, xe) => rf.Items.Add(RiverItem.LoadItem(xe)) },
            };


        public static RiverFeed LoadFeed(Uri feedUrl, XElement item)
        {
            var rf = new RiverFeed();
            foreach (XElement xe in item.Elements())
            {
                Action<RiverFeed, XElement> action;
                if (FeedElements.TryGetValue(xe.Name, out action)) { action(rf, xe); }
            }
            rf.FeedUrl = feedUrl;
            return rf;
        }

        static void HandleAtomLink(RiverFeed feed, XElement link)
        {
            if (
                link.Attribute(XNames.Atom.Rel)?.Value == "alternate"
                && link.Attribute(XNames.Atom.Type)?.Value == "text/html"
            )
            {
                feed.WebsiteUrl = link.Attribute(XNames.Atom.Href)?.Value;
            }
        }
    }

    // TODO: Relative URLs.

    class RiverItem
    {
        readonly List<RiverItemEnclosure> enclosures = new List<RiverItemEnclosure>();

        public string Title { get; set; } = String.Empty;
        public string Link { get; set; } = String.Empty;
        public string Body { get; set; } = String.Empty;
        public DateTime? PubDate { get; set; }
        public string PermaLink { get; set; } = String.Empty;
        public string Comments { get; set; } = String.Empty;
        public string Id { get; set; } = String.Empty;
        public RiverItemThumbnail Thumbnail { get; set; }
        public List<RiverItemEnclosure> Enclosures => this.enclosures;

        static readonly Dictionary<XName, Action<RiverItem, XElement>> ItemElements =
            new Dictionary<XName, Action<RiverItem, XElement>>
            {
                { XNames.RSS.Title,       (ri, xe) => ri.Title = Util.ParseBody(xe) },
                { XNames.RSS.Link,        (ri, xe) => ri.Link = xe.Value },
                { XNames.RSS.Description, (ri, xe) => ri.Body = Util.ParseBody(xe) },
                { XNames.RSS.Comments,    (ri, xe) => ri.Comments = xe.Value },
                { XNames.RSS.PubDate,     (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.RSS.Guid,        (ri, xe) => HandleGuid(ri, xe) },
                { XNames.RSS.Enclosure,   (ri, xe) => HandleEnclosure(ri, xe) },

                { XNames.RSS10.Title,       (ri, xe) => ri.Title = Util.ParseBody(xe) },
                { XNames.RSS10.Link,        (ri, xe) => ri.Link = xe.Value },
                { XNames.RSS10.Description, (ri, xe) => ri.Body = Util.ParseBody(xe) },
                { XNames.RSS10.Comments,    (ri, xe) => ri.Comments = xe.Value },
                { XNames.RSS10.PubDate,     (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.RSS10.Guid,        (ri, xe) => HandleGuid(ri, xe) },

                { XNames.Atom.Title,       (ri, xe) => ri.Title = Util.ParseBody(xe) },
                { XNames.Atom.Content,     (ri, xe) => ri.Body = Util.ParseBody(xe) },
                { XNames.Atom.Summary,     (ri, xe) => ri.Body = Util.ParseBody(xe) },
                { XNames.Atom.Link,        (ri, xe) => HandleAtomLink(ri, xe) },
                { XNames.Atom.Id,          (ri, xe) => ri.Id = xe.Value },
                { XNames.Atom.Published,   (ri, xe) => HandlePubDate(ri, xe) },
                { XNames.Atom.Updated,     (ri, xe) => HandlePubDate(ri, xe) },
            };

        static void HandleGuid(RiverItem item, XElement element)
        {
            item.Id = element.Value;
            if (item.Id.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                item.Id.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                item.PermaLink = item.Id;
            }
        }

        static void HandleEnclosure(RiverItem item, XElement element)
        {
            item.Enclosures.Add(new RiverItemEnclosure
            {
                Length = element.Attribute(XNames.RSS.Length)?.Value,
                Type = element.Attribute(XNames.RSS.Type)?.Value,
                Url = element.Attribute(XNames.RSS.Url)?.Value,
            });
        }

        static void HandlePubDate(RiverItem item, XElement element)
        {
            DateTime? date = Util.ParseDate(element);
            if (date != null && (item.PubDate == null || date > item.PubDate))
            {
                item.PubDate = date;
            }
        }

        static void HandleAtomLink(RiverItem item, XElement link)
        {
            if (link.Attribute(XNames.Atom.Rel)?.Value == "alternate" &&
                link.Attribute(XNames.Atom.Type)?.Value == "text/html")
            {
                item.Link = link.Attribute(XNames.Atom.Href)?.Value;
            }

            if (link.Attribute(XNames.Atom.Rel)?.Value == "self" &&
                link.Attribute(XNames.Atom.Type)?.Value == "text/html")
            {
                item.PermaLink = link.Attribute(XNames.Atom.Href)?.Value;
            }

            if (link.Attribute(XNames.Atom.Rel)?.Value == "enclosure")
            {
                item.Enclosures.Add(new RiverItemEnclosure
                {
                    Length = link.Attribute(XNames.Atom.Length)?.Value,
                    Type = link.Attribute(XNames.Atom.Type)?.Value,
                    Url = link.Attribute(XNames.Atom.Href)?.Value,
                });
            }
        }

        public static RiverItem LoadItem(XElement item)
        {
            var ri = new RiverItem();
            foreach (XElement xe in item.Elements())
            {
                Action<RiverItem, XElement> action;
                if (ItemElements.TryGetValue(xe.Name, out action)) { action(ri, xe); }
            }

            if (ri.PermaLink == null) { ri.PermaLink = ri.Link; }
            if (ri.Id == null) { ri.Id = CreateId(ri); }
            if (ri.Thumbnail == null)
            {
                // Load the thumbnail.
            }
            return ri;
        }

        static string CreateId(RiverItem item)
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
    }

    class RiverItemThumbnail
    {
        public string Url { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    class RiverItemEnclosure
    {
        public string Url { get; set; }
        public string Type { get; set; }
        public string Length { get; set; }
    }

    class Feed
    {
        public static async Task<RiverFeed> GetFeedAsync(
            Uri uri,
            string etag,
            string ifModifiedSince,
            CancellationToken cancellationToken)
        {
            Stopwatch loadTimer = Stopwatch.StartNew();
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TheOnceAndFuture/1.0");

                Log.BeginGetFeed(uri);
                HttpResponseMessage response = await client.GetAsync(uri, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    //string body = await response.Content.ReadAsStringAsync();
                    string body = string.Empty;
                    Log.EndGetFeedFailure(uri, response, body, loadTimer);
                    return null;
                }

                Uri responseUri = response.RequestMessage.RequestUri;

                // TODO: Character detection!
                // TODO: Handle redirection correctly.

                await response.Content.LoadIntoBufferAsync();
                using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                using (var textReader = new StreamReader(responseStream))
                using (var reader = XmlReader.Create(textReader, null, responseUri.AbsoluteUri))
                {
                    XElement element = XElement.Load(reader, LoadOptions.SetBaseUri);
                    if (element.Name == XNames.RSS.Rss)
                    {
                        RiverFeed result = RiverFeed.LoadFeed(responseUri, element.Element(XNames.RSS.Channel));
                        Log.EndGetFeed(uri, "rss2.0", response, result, loadTimer);
                        return result;
                    }
                    else if (element.Name == XNames.Atom.Feed)
                    {
                        RiverFeed result = RiverFeed.LoadFeed(responseUri, element);
                        Log.EndGetFeed(uri, "atom", response, result, loadTimer);
                        return result;
                    }
                    else if (element.Name == XNames.RDF.Rdf)
                    {
                        RiverFeed result = RiverFeed.LoadFeed(responseUri, element.Element(XNames.RSS10.Channel));
                        foreach (XElement elem in element.Elements(XNames.RSS10.Item))
                        {
                            result.Items.Add(RiverItem.LoadItem(elem));
                        }
                        Log.EndGetFeed(uri, "rdf", response, result, loadTimer);
                        return result;
                    }
                    else
                    {
                        Log.UnrecognizableFeed(uri, response, element.ToString(), loadTimer);
                        return null;
                    }
                }
            }
            catch (HttpRequestException requestException)
            {
                Log.NetworkError(uri, requestException, loadTimer);
                return null;
            }
            catch (XmlException xmlException)
            {
                Log.XmlError(uri, xmlException, loadTimer);
                return null;
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                XDocument doc = XDocument.Load(@"C:\Users\John\Downloads\NewsBlur-DeCarabas-2016-11-08");
                XElement body = doc.Root.Element(XNames.OPML.Body);

                List<OpmlEntry> feeds =
                    (from elem in body.Descendants(XNames.OPML.Outline)
                     where elem.Attribute(XNames.OPML.XmlUrl) != null
                     select OpmlEntry.FromXml(elem)).ToList();

                var parses =
                    from entry in feeds
                    select new
                    {
                        url = entry.XmlUrl,
                        task = Feed.GetFeedAsync(entry.XmlUrl, null, null, CancellationToken.None),
                    };

                foreach (var parse in parses.ToList())
                {
                    parse.task.Wait();

                    Console.WriteLine(parse.url);
                    DumpFeed(parse.task.Result);
                    Console.ReadLine();
                }

                //if (Util.BadDates.Count > 0)
                //{
                //    Console.WriteLine("BAD DATE:");
                //    Console.WriteLine(String.Join("\n", Util.BadDates));
                //}
                //else
                //{
                //    Console.WriteLine("NO BAD DATES");
                //}

                //Task<RiverFeed>[] tasks =
                //    feeds.Select(f => Feed.LoadFeed(f.XmlUrl, null, null, CancellationToken.None)).ToArray();
                //RiverFeed[] riverFeeds = Task.WhenAll(tasks).Result;
                //foreach (RiverFeed riverFeed in riverFeeds)
                //{
                //    DumpFeed(riverFeed);
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
