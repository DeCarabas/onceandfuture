namespace OnceAndFuture.Syndication
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    public class UpdatedFeeds
    {
        public UpdatedFeeds(IEnumerable<FeedSegment> feeds = null)
        {
            Feeds =
                ImmutableList.CreateRange<FeedSegment>(
                    feeds ?? Enumerable.Empty<FeedSegment>()
                );
        }

        public UpdatedFeeds With(IEnumerable<FeedSegment> feeds)
        {
            return new UpdatedFeeds(feeds);
        }

        [JsonProperty(PropertyName = "updatedFeed")]
        public ImmutableList<FeedSegment> Feeds { get; }
    }
}
