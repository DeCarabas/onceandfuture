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
    using System.Xml;
    using Polly;
    using Serilog;

    static class Log
    {
        readonly static ConcurrentDictionary<string, ILogger> TaggedLoggers =
            new ConcurrentDictionary<string, ILogger>();
        readonly static Func<string, ILogger> logCreator = Create;

        static ILogger Create(string tag) => Serilog.Log.Logger.ForContext("tag", tag);

        public static ILogger Get([CallerMemberName]string tag = null) => TaggedLoggers.GetOrAdd(tag, logCreator);

        public static void BadDate(string url, string date)
        {
            Get().Warning("{url}: Unparsable date encountered: {date}", url, date);
        }

        public static void NetworkError(Uri uri, Exception e, Stopwatch loadTimer)
        {
            Get().Error(e, "{uri}: {elapsed} ms: Network Error", uri, loadTimer.ElapsedMilliseconds);
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
                exception, "HTTP error detected from {url}, retry {retry} after {ts}", url, retryCount, timespan);
        }

        public static void PutObjectComplete(string bucket, string name, string type, Stopwatch timer, Stream stream)
        {
            Get().Verbose(
                "Put Object: {bucket}/{name} ({type}, {len} bytes) in {elapsed}ms",
                bucket, name, type, stream.Length, timer.ElapsedMilliseconds
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
            string bucket, string name, string type, Exception error, Stopwatch timer, string code, string body)
        {
            Get().Error(
                error,
                "Put Object: ERROR {bucket}/{name} ({type}) in {elapsed}ms: {code}: {body}",
                bucket, name, type, timer.ElapsedMilliseconds, code, body
            );
        }

        public static void GetObjectError(
            string bucket, string name, Exception error, Stopwatch timer, string code, string body)
        {
            Get().Error(
                error,
                "Get Object: ERROR {bucket}/{name} in {elapsed}ms: {code}: {body}",
                bucket, name, timer.ElapsedMilliseconds, code, body
            );
        }

        public static void GetObjectNotFound(string bucket, string name, Stopwatch timer)
        {
            Get().Information(
                "Object {name} not found in S3 bucket {bucket} ({elapsed}ms)",
                name, bucket, timer.ElapsedMilliseconds
            );
        }

        public static void DetectFeedServerError(Uri uri, HttpResponseMessage response)
        {
            Get().Warning("Error detecting feed @ {url}: {status}", uri.AbsoluteUri, response.StatusCode);
        }

        public static void DetectFeedLoadFeedError(Uri feedUri, HttpStatusCode lastStatus)
        {
            Get().Warning("Error loading detected feed @ {url}: {status}", feedUri.AbsoluteUri, lastStatus);
        }

        public static void FindFeedBaseWasFeed(Uri baseUri)
        {
            Get().Debug("{base}: Base URL was a feed.", baseUri.AbsoluteUri);
        }

        public static void FindFeedCheckingBase(Uri baseUri)
        {
            Get().Debug("{base}: Checking base URL...", baseUri.AbsoluteUri);
        }

        public static void FindFeedCheckingLinkElements(Uri baseUri)
        {
            Get().Debug("{base}: Checking link elements...", baseUri.AbsoluteUri);
        }

        public static void FindFeedFoundLinkElements(Uri baseUri, List<Uri> linkUrls)
        {
            Get().Debug("{base}: Found {count} link elements.", baseUri.AbsoluteUri, linkUrls.Count);
        }

        public static void FindFeedCheckingAnchorElements(Uri baseUri)
        {
            Get().Debug("{base}: Checking anchor elements...", baseUri.AbsoluteUri);
        }

        public static void FindFeedFoundSomeAnchors(Uri baseUri, List<Uri> localGuesses, List<Uri> remoteGuesses)
        {
            Get().Debug("{base}: Found {localCount} local and {remoteCount} remote anchors.",
                baseUri.AbsoluteUri, localGuesses.Count, remoteGuesses.Count);
        }

        public static void FindFeedsFoundLocalGuesses(Uri baseUri, List<Uri> localAnchors)
        {
            Get().Debug("{base}: Found {count} local anchors.", baseUri.AbsoluteUri, localAnchors.Count);
        }

        public static void FindFeedsFoundRemoteGuesses(Uri baseUri, List<Uri> remoteAnchors)
        {
            Get().Debug("{base}: Found {count} remote anchors.", baseUri.AbsoluteUri, remoteAnchors.Count);
        }

        public static void FindFeedsFoundRandomGuesses(Uri baseUri, List<Uri> randomGuesses)
        {
            Get().Debug("{base}: Found {count} random guesses.", baseUri.AbsoluteUri, randomGuesses.Count);
        }

        public static void FindFeedFoundTotal(Uri baseUri, List<Uri> allUrls)
        {
            Get().Debug("{base}: Found {count} URIs in total.", baseUri.AbsoluteUri, allUrls.Count);
        }

        public static void WroteArchive(string id, River river, string archiveKey) =>
            Get().Information("{id}: Wrote archive at {archiveKey}", id, archiveKey);

        public static void SplittingFeed(string id, River river) =>
            Get().Verbose("{id}: Splitting feed with {count} items in it...", id, river.UpdatedFeeds.Feeds.Count);
    }
}
