namespace OnceAndFuture.Syndication
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    [JsonObject]
    public class FeedSegment
    {
        public FeedSegment(
            string feedTitle = null,
            Uri feedUrl = null,
            string websiteUrl = null,
            string feedDescription = null,
            DateTimeOffset? whenLastUpdate = null,
            IEnumerable<Item> items = null)
        {
            FeedTitle = feedTitle ?? String.Empty;
            FeedUrl = feedUrl;
            WebsiteUrl = websiteUrl ?? String.Empty;
            FeedDescription = feedDescription ?? String.Empty;
            WhenLastUpdate = whenLastUpdate ?? DateTimeOffset.UtcNow;

            Items = ImmutableList.CreateRange<Item>(items ?? Enumerable.Empty<Item>());
        }

        public FeedSegment With(
            string feedTitle = null,
            Uri feedUrl = null,
            string websiteUrl = null,
            string feedDescription = null,
            DateTimeOffset? whenLastUpdate = null,
            IEnumerable<Item> items = null)
        {
            return new FeedSegment(
                feedTitle ?? FeedTitle,
                feedUrl ?? FeedUrl,
                websiteUrl ?? WebsiteUrl,
                feedDescription ?? FeedDescription,
                whenLastUpdate ?? WhenLastUpdate,
                items ?? Items);
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
        public DateTimeOffset WhenLastUpdate { get; }

        [JsonProperty(PropertyName = "item")]
        public ImmutableList<Item> Items { get; }
    }
}
