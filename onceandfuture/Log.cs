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
    using System.Threading.Tasks;
    using System.Xml;
    using Polly;
    using Serilog;
    using Serilog.Events;

    static class Log
    {
        static readonly ConcurrentDictionary<string, ILogger> TaggedLoggers =
            new ConcurrentDictionary<string, ILogger>();
        static readonly Func<string, ILogger> LogCreator = Create;

        static ILogger Create(string tag) => Serilog.Log.Logger.ForContext("tag", tag);

        static ILogger Get([CallerMemberName]string tag = "unknown") => TaggedLoggers.GetOrAdd(tag, LogCreator);

        public static void BadDate(string url, string date)
        {
            Get().Warning("{FeedUrl}: Unparsable date encountered: {DateString}", url, date);
        }

        public static void NetworkError(Uri uri, Exception e, Stopwatch loadTimer)
            => EndGetFeed(uri, "network_error", null, null, null, loadTimer, e);

        public static void XmlError(Uri uri, XmlException xmlException, Stopwatch loadTimer)
            => EndGetFeed(uri, "xml_error", null, null, null, loadTimer, xmlException);

        public static void BeginGetFeed(Uri uri)
        {
            Get().Information("{FeedUrl}: Begin fetching feed", uri);
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
                "{FeedUrl}: {FeedStatus}: Fetched {ItemCount} items from {Version} feed in {ElapsedMs} ms",
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

        public static void FoundThumbnail(Uri baseUrl, Uri uri, string kind)
        {
            Get().Information("{BaseUrl}: Found thumbnail {ImageUrl} ({ThumbKind})", baseUrl, uri, kind);
        }

        public static void BeginLoadThumbnails(Uri baseUri)
        {
            Get().Information("{BaseUrl}: Loading thumbnails...", baseUri);
        }

        public static void EndLoadThumbnails(Uri baseUri, RiverItem[] items, Stopwatch loadTimer)
        {
            Get().Information(
                "{BaseUrl}: Finished loading thumbs for {Count} items in {ElapsedMs} ms",
                baseUri,
                items.Length,
                loadTimer.ElapsedMilliseconds);
        }

        public static void NoThumbnailFound(Uri baseUrl)
        {
            Get().Information("{BaseUrl}: No suitable thumbnails found.", baseUrl);
        }

        public static void EndGetThumbsFromSoup(Uri baseUrl, int length, Stopwatch loadTimer)
        {
            Get().Information(
                "{BaseUrl}: Loaded {Count} thumbnails in {ElapsedMs} ms",
                baseUrl, length, loadTimer.ElapsedMilliseconds);
        }

        public static void BeginGetThumbsFromSoup(Uri baseUrl, int length)
        {
            Get().Information("{BaseUrl}: Checking {Count} thumbnails...", baseUrl, length);
        }

        public static void ThumbnailSuccessCacheHit(Uri baseUrl, Uri imageUrl)
        {
            Get().Information("{BaseUrl}: {ImageUrl}: Cached Success", baseUrl, imageUrl);
        }

        public static void ThumbnailErrorCacheHit(Uri baseUrl, Uri imageUrl, object cachedObject)
        {
            Get().Information(
                "{BaseUrl}: {ImageUrl}: Cached Error: {ErrorMessage}",
                baseUrl,
                imageUrl,
                (string)cachedObject);
        }

        public static void FeedTimeout(Uri uri, Stopwatch loadTimer, Exception error)
            => EndGetFeed(uri, "timeout", null, null, null, loadTimer, error);

        public static void FindThumbnailNetworkError(Uri uri, Exception e)
        {
            Get().Error(e, "{BaseUrl}: Network Error", uri);
        }

        public static void FindThumbnailTimeout(Uri uri)
        {
            Get().Error("{BaseUrl}: Timeout", uri);
        }

        public static void FindThumbnailServerError(Uri uri, HttpResponseMessage response)
        {
            Get().Error(
                "{BaseUrl}: Server error: {HttpStatusCode} {ReasonPhrase}",
                uri,
                response.StatusCode,
                response.ReasonPhrase);
        }

        public static void DetectFeedServerError(Uri uri, HttpResponseMessage response)
            => Get().Warning("Error detecting feed @ {Url}: {HttpStatusCode}", uri.AbsoluteUri, response.StatusCode);

        public static void FindFeedBaseWasFeed(Uri baseUri)
            => Get().Debug("{BaseUrl}: Base URL was a feed.", baseUri.AbsoluteUri);

        public static void FindFeedCheckingBase(Uri baseUri)
            => Get().Debug("{BaseUrl}: Checking base URL...", baseUri.AbsoluteUri);

        public static void FindFeedCheckingLinkElements(Uri baseUri)
            => Get().Debug("{BaseUrl}: Checking link elements...", baseUri.AbsoluteUri);

        public static void FindFeedFoundLinkElements(Uri baseUri, List<Uri> linkUrls)
            => Get().Debug("{BaseUrl}: Found {Count} link elements.", baseUri.AbsoluteUri, linkUrls.Count);

        public static void FindFeedCheckingAnchorElements(Uri baseUri)
            => Get().Debug("{BaseUrl}: Checking anchor elements...", baseUri.AbsoluteUri);

        public static void FindFeedFoundSomeAnchors(Uri baseUri, List<Uri> localGuesses, List<Uri> remoteGuesses)
            => Get().Debug(
                "{BaseUrl}: Found {LocalCount} local and {RemoteCount} remote anchors.",
                baseUri.AbsoluteUri, localGuesses.Count, remoteGuesses.Count);

        public static void FindFeedsFoundLocalGuesses(Uri baseUri, List<Uri> localAnchors)
            => Get().Debug("{BaseUrl}: Found {Count} local anchors.", baseUri.AbsoluteUri, localAnchors.Count);

        public static void FindFeedsFoundRemoteGuesses(Uri baseUri, List<Uri> remoteAnchors)
            => Get().Debug("{BaseUrl}: Found {Count} remote anchors.", baseUri.AbsoluteUri, remoteAnchors.Count);

        public static void FindFeedsFoundRandomGuesses(Uri baseUri, List<Uri> randomGuesses)
            => Get().Debug("{BaseUrl}: Found {Count} random guesses.", baseUri.AbsoluteUri, randomGuesses.Count);

        public static void FindFeedFoundTotal(Uri baseUri, List<Uri> allUrls)
            => Get().Debug("{BaseUrl}: Found {Count} URIs in total.", baseUri.AbsoluteUri, allUrls.Count);

        public static void WroteArchive(string id, River river, string archiveKey)
            => Get().Information("{AggregateId}: Wrote archive at {ArchiveKey}", id, archiveKey);

        public static void SplittingFeed(string id, River river)
            => Get().Verbose(
                "{AggregateId}: Splitting feed with {FeedCount} items in it...",
                id,
                river.UpdatedFeeds.Feeds.Count);

        public static void AggregateRefreshed(string id, Stopwatch aggregateTimer)
            => Get().Information("{AggregateId}: Refreshed in {ElapsedMs}ms", id, aggregateTimer.ElapsedMilliseconds);

        public static void UpdatingAggregate(string id) => Get().Information("{AggregateId}: Updating aggregate", id);

        public static void AggregateHasNewFeeds(string id, int newFeedsCount)
            => Get().Information("{AggregateId}: Resulted in {RiverCount} new feeds", id, newFeedsCount);

        public static void AggregateFeedState(
            string id, Uri metadataOriginUrl, int newUpdatesLength, DateTimeOffset biggestUpdate)
            => Get().Debug(
                "{AggregateId}: {FeedUrl}: Has {Count} new items @ {LastUpdate}",
                id, metadataOriginUrl, newUpdatesLength, biggestUpdate);

        public static void AggregateNewUpdates(string id, Uri metadataOriginUrl, int newUpdatesLength)
            => Get().Debug(
                "{AggregateId}: {FeedUrl}: Has {Count} new updates",
                id,
                metadataOriginUrl,
                newUpdatesLength);

        public static void AggregateRefreshPulledRivers(string id, int riversLength)
            => Get().Information("{AggregateId}: Pulled {RiverCount} rivers", id, riversLength);

        public static void AggregateRefreshStart(string id, int feedUrlsCount)
            => Get().Information("{AggregateId}: Refreshing aggregate with {FeedUrlCount} feeds", id, feedUrlsCount);

        public static void AsyncProgressTaskComplete(Task task, string description)
            => Get().Write(
                task.IsFaulted ? LogEventLevel.Error : LogEventLevel.Information,
                task.Exception,
                "Progress: Task '{Description}': {Status}",
                description, task.Status.ToString());
    }
}
