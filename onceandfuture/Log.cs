namespace onceandfuture
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Polly;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.PeriodicBatching;
    using System.Linq;
    using Serilog.Configuration;

    static class Log
    {
        static readonly ConcurrentDictionary<string, ILogger> TaggedLoggers =
            new ConcurrentDictionary<string, ILogger>();
        static readonly Func<string, ILogger> LogCreator = Create;

        static ILogger Create(string tag) => Serilog.Log.Logger.ForContext("tag", tag);

        static ILogger Get([CallerMemberName]string tag = "unknown") => TaggedLoggers.GetOrAdd(tag, LogCreator);

        public static void BadDate(string url, string date)
        {
            Get().Warning("{url}: Unparsable date encountered: {date}", url, date);
        }

        public static void NetworkError(Uri uri, Exception e, Stopwatch loadTimer)
            => EndGetFeed(uri, "network_error", null, null, null, loadTimer, e);

        public static void XmlError(Uri uri, XmlException xmlException, Stopwatch loadTimer)
            => EndGetFeed(uri, "xml_error", null, null, null, loadTimer, xmlException);

        public static void BeginGetFeed(Uri uri)
        {
            Get().Information("{url}: Begin fetching feed", uri);
        }

        public static void EndGetFeed(
            Uri uri,
            string status,
            string version,
            HttpResponseMessage response,
            RiverFeed result,
            Stopwatch loadTimer,
            Exception error = null
        )
        {
            Get().Information(
                error,
                "{url}: {status}: Fetched {item_count} items from {version} feed in {elapsed_ms} ms",
                uri,
                status,
                result?.Items?.Count ?? 0,
                version,
                loadTimer.ElapsedMilliseconds
            );
        }

        public static void UnrecognizableFeed(Uri uri, HttpResponseMessage response, string body, Stopwatch loadTimer)
            => EndGetFeed(uri, "unrecognizable", "???", response, null, loadTimer); // TODO: Body

        public static void EndGetFeedFailure(Uri uri, HttpResponseMessage response, string body, Stopwatch loadTimer)
            => EndGetFeed(uri, "failure", null, response, null, loadTimer, null); // TODO: Body

        public static void EndGetFeedNotModified(Uri uri, HttpResponseMessage response, Stopwatch loadTimer)
            => EndGetFeed(uri, "not_modified", null, response, null, loadTimer, null);

        public static void EndGetFeedMovedPermanently(Uri uri, HttpResponseMessage response, Stopwatch loadTimer)
            => EndGetFeed(uri, "moved", null, response, null, loadTimer, null);

        public static void ConsideringImage(Uri baseUrl, Uri uri, string kind, int area, float ratio)
        {
            Get().Information(
                "{baseUrl}: Considering image {url} ({kind}) (area: {area}, ratio: {ratio})",
                baseUrl, uri, kind, area, ratio);
        }

        public static void GetObjectAccessDenied(string bucket, string key, Stopwatch timer)
            => Get().Information(
                "Object {key} access denied in S3 bucket {bucket} ({elapsed_ms}ms)",
                key, bucket, timer.ElapsedMilliseconds
            );

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

        public static void ThumbnailNetworkError(Uri baseUrl, Uri imageUri, string kind, Exception e)
        {
            Get().Error(e, "{baseUrl}: {url} ({kind}): Network Error", baseUrl, imageUri, kind);
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
                "{baseUrl}: Finished loading thumbs for {count} items in {elapsed_ms} ms",
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
                "{baseUrl}: Loaded {length} thumbnails in {elapsed_ms} ms",
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

        public static void FeedTimeout(Uri uri, Stopwatch loadTimer, Exception error)
            => EndGetFeed(uri, "timeout", null, null, null, loadTimer, error);

        public static void ThumbnailTimeout(Uri baseUrl, Uri imageUri, string kind)
        {
            Get().Error("{baseUrl}: {url} ({kind}): Timeout", baseUrl, imageUri, kind);
        }

        public static void FindThumbnailNetworkError(Uri uri, Exception e)
        {
            Get().Error(e, "{url}: Network Error", uri);
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
                exception,
                "HTTP error detected from {url}, sleeping {ts} before retry {retry}",
                url, timespan, retryCount);
        }

        public static void PutObjectComplete(string bucket, string name, string type, Stopwatch timer, Stream stream)
        {
            Get().Verbose(
                "Put Object: {bucket}/{name} ({type}, {len} bytes) in {elapsed_ms}ms",
                bucket, name, type, stream.Length, timer.ElapsedMilliseconds
            );
        }

        public static void GetObjectComplete(string bucket, string name, Stopwatch timer)
        {
            Get().Verbose(
                "Get Object: {bucket}/{name} in {elapsed_ms}ms",
                bucket, name, timer.ElapsedMilliseconds
            );
        }

        public static void PutObjectError(
            string bucket, string name, string type, Exception error, Stopwatch timer, string code, string body)
        {
            Get().Error(
                error,
                "Put Object: ERROR {bucket}/{name} ({type}) in {elapsed_ms}ms: {code}: {body}",
                bucket, name, type, timer.ElapsedMilliseconds, code, body
            );
        }

        public static void GetObjectError(
            string bucket, string name, Exception error, Stopwatch timer, string code, string body)
        {
            Get().Error(
                error,
                "Get Object: ERROR {bucket}/{name} in {elapsed_ms}ms: {code}: {body}",
                bucket, name, timer.ElapsedMilliseconds, code, body
            );
        }

        public static void GetObjectNotFound(string bucket, string name, Stopwatch timer)
            => Get().Information(
                "Object {name} not found in S3 bucket {bucket} ({elapsed_ms}ms)",
                name, bucket, timer.ElapsedMilliseconds);

        public static void DetectFeedServerError(Uri uri, HttpResponseMessage response)
            => Get().Warning("Error detecting feed @ {url}: {status}", uri.AbsoluteUri, response.StatusCode);

        public static void DetectFeedLoadFeedError(Uri feedUri, HttpStatusCode lastStatus)
            => Get().Warning("Error loading detected feed @ {url}: {status}", feedUri.AbsoluteUri, lastStatus);

        public static void FindFeedBaseWasFeed(Uri baseUri)
            => Get().Debug("{base}: Base URL was a feed.", baseUri.AbsoluteUri);

        public static void FindFeedCheckingBase(Uri baseUri)
            => Get().Debug("{base}: Checking base URL...", baseUri.AbsoluteUri);

        public static void FindFeedCheckingLinkElements(Uri baseUri)
            => Get().Debug("{base}: Checking link elements...", baseUri.AbsoluteUri);

        public static void FindFeedFoundLinkElements(Uri baseUri, List<Uri> linkUrls)
            => Get().Debug("{base}: Found {count} link elements.", baseUri.AbsoluteUri, linkUrls.Count);

        public static void FindFeedCheckingAnchorElements(Uri baseUri)
            => Get().Debug("{base}: Checking anchor elements...", baseUri.AbsoluteUri);

        public static void FindFeedFoundSomeAnchors(Uri baseUri, List<Uri> localGuesses, List<Uri> remoteGuesses)
            => Get().Debug(
                "{base}: Found {localCount} local and {remoteCount} remote anchors.",
                baseUri.AbsoluteUri, localGuesses.Count, remoteGuesses.Count);

        public static void FindFeedsFoundLocalGuesses(Uri baseUri, List<Uri> localAnchors)
            => Get().Debug("{base}: Found {count} local anchors.", baseUri.AbsoluteUri, localAnchors.Count);

        public static void FindFeedsFoundRemoteGuesses(Uri baseUri, List<Uri> remoteAnchors)
            => Get().Debug("{base}: Found {count} remote anchors.", baseUri.AbsoluteUri, remoteAnchors.Count);

        public static void FindFeedsFoundRandomGuesses(Uri baseUri, List<Uri> randomGuesses)
            => Get().Debug("{base}: Found {count} random guesses.", baseUri.AbsoluteUri, randomGuesses.Count);

        public static void FindFeedFoundTotal(Uri baseUri, List<Uri> allUrls)
            => Get().Debug("{base}: Found {count} URIs in total.", baseUri.AbsoluteUri, allUrls.Count);

        public static void WroteArchive(string id, River river, string archiveKey)
            => Get().Information("{id}: Wrote archive at {archiveKey}", id, archiveKey);

        public static void SplittingFeed(string id, River river)
            => Get().Verbose("{id}: Splitting feed with {count} items in it...", id, river.UpdatedFeeds.Feeds.Count);

        public static void AggregateRefreshed(string id, Stopwatch aggregateTimer)
            => Get().Information("{id}: Refreshed in {elapsed_ms}ms", id, aggregateTimer.ElapsedMilliseconds);

        public static void UpdatingAggregate(string id) => Get().Information("{id}: Updating aggregate", id);

        public static void AggregateHasNewFeeds(string id, int newFeedsCount)
            => Get().Information("{id}: Resulted in {riverCount} new feeds", id, newFeedsCount);

        public static void AggregateFeedState(
            string id, Uri metadataOriginUrl, int newUpdatesLength, DateTimeOffset biggestUpdate)
            => Get().Debug(
                "{id}: {feedUrl}: Has {count} new items @ {lastUpdate}",
                id, metadataOriginUrl, newUpdatesLength, biggestUpdate);

        public static void AggregateNewUpdates(string id, Uri metadataOriginUrl, int newUpdatesLength)
            => Get().Debug("{id}: {feedUrl}: Has {count} new updates", id, metadataOriginUrl, newUpdatesLength);

        public static void AggregateRefreshPulledRivers(string id, int riversLength)
            => Get().Information("{id}: Pulled {riverCount} rivers", id, riversLength);

        public static void AggregateRefreshStart(string id, int feedUrlsCount)
            => Get().Information("{id}: Refreshing aggregate with {feedUrlCount} feeds", id, feedUrlsCount);
    }

    public class HoneycombSink : PeriodicBatchingSink
    {
        HttpClient client;
        string dataset;

        public HoneycombSink(string defaultDataset, string writeKey, int batchSizeLimit, TimeSpan period)
            : base(batchSizeLimit, period)
        {
            this.client = new HttpClient();
            this.client.BaseAddress = new Uri("https://api.honeycomb.io/1/");
            this.client.DefaultRequestHeaders.Add("X-Honeycomb-Team", writeKey);

            this.dataset = defaultDataset;

        }

        protected override bool CanInclude(LogEvent evt)
        {
            return base.CanInclude(evt);
        }

        protected override Task EmitBatchAsync(IEnumerable<LogEvent> events)
            => Task.WhenAll(events.Select(e => SendEvent(e)).ToArray());

        async Task SendEvent(LogEvent logEvent)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "events/" + this.dataset)
            {
                Content = new StringContent(
                    content: JsonConvert.SerializeObject(EventToJObject(logEvent)),
                    encoding: Encoding.UTF8,
                    mediaType: "application/json"
                ),
                Headers =
                {
                    { "X-Honeycomb-Event-Time", logEvent.Timestamp.ToString("o") },
                }
            };

            using (HttpResponseMessage message = await client.SendAsync(request))
            {
                // TODO: Log if unsuccessful. This requires... hum.
                if (!message.IsSuccessStatusCode)
                {
                    string msg = await message.Content.ReadAsStringAsync();
                    Console.WriteLine($"Warning: Failed to send event: {message.StatusCode}: {msg}");
                }
            }
        }

        public static JObject EventToJObject(LogEvent logEvent)
        {
            JObject evt = new JObject(
                new JProperty("message", logEvent.RenderMessage()),
                new JProperty("level", logEvent.Level.ToString())
            );
            foreach (KeyValuePair<string, LogEventPropertyValue> prop in logEvent.Properties)
            {
                evt[prop.Key] = ConvertPropertyValue(prop.Value);
            }

            return evt;
        }

        static JToken ConvertPropertyValue(LogEventPropertyValue value)
        {
            var scalar = value as ScalarValue;
            if (scalar != null) { return ConvertScalarValue(scalar); }

            var seq = value as SequenceValue;
            if (seq != null) { return ConvertSequenceValue(seq); }

            var strct = value as StructureValue;
            if (strct != null) { return ConvertStructureValue(strct); }

            var dict = value as DictionaryValue;
            if (dict != null) { return ConvertDictionaryValue(dict); }

            // TODO: Log or something.
            return JValue.CreateUndefined();
        }

        static JToken ConvertDictionaryValue(DictionaryValue dict)
        {
            JObject obj = new JObject();
            foreach (KeyValuePair<ScalarValue, LogEventPropertyValue> kvp in dict.Elements)
            {
                string propname = kvp.Key.Value?.ToString() ?? "<<null>>";
                obj.Add(propname, ConvertPropertyValue(kvp.Value));
            }
            return obj;
        }

        static JToken ConvertStructureValue(StructureValue strct)
        {
            JObject obj = new JObject();
            obj.Add("__type", new JValue(strct.TypeTag));
            foreach (LogEventProperty prop in strct.Properties)
            {
                obj.Add(prop.Name, ConvertPropertyValue(prop.Value));
            }
            return obj;
        }

        static JToken ConvertSequenceValue(SequenceValue seq)
        {
            JArray arr = new JArray();
            foreach (LogEventPropertyValue elem in seq.Elements)
            {
                arr.Add(ConvertPropertyValue(elem));
            }
            return arr;
        }

        static JToken ConvertScalarValue(ScalarValue scalar)
        {
            return new JValue(scalar.Value);
        }
    }

    public static class LoggerConfigurationHoneycombExtensions
    {
        public static LoggerConfiguration Honeycomb(
            this LoggerSinkConfiguration sinkConfiguration,
            string dataset,
            string writeKey,
            int batchSizeLimit = 50,
            TimeSpan? period = null,
            LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
        {
            return sinkConfiguration.Sink(
                new HoneycombSink(dataset, writeKey, batchSizeLimit, period ?? TimeSpan.FromMinutes(1)),
                restrictedToMinimumLevel
            );
        }
    }

}
