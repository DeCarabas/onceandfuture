namespace OnceAndFuture.Syndication
{
    using Newtonsoft.Json;

    public class River
    {
        public River(
            UpdatedFeeds updatedFeeds = null,
            RiverFeedMeta metadata = null)
        {
            UpdatedFeeds = updatedFeeds ?? new UpdatedFeeds();
            Metadata = metadata ?? new RiverFeedMeta();
        }

        public River With(
            UpdatedFeeds updatedFeeds = null,
            RiverFeedMeta metadata = null)
        {
            return new River(updatedFeeds ?? UpdatedFeeds, metadata ?? Metadata);
        }

        [JsonProperty(PropertyName = "updatedFeeds")]
        public UpdatedFeeds UpdatedFeeds { get; }

        [JsonProperty(PropertyName = "metadata")]
        public RiverFeedMeta Metadata { get; }
    }
}
