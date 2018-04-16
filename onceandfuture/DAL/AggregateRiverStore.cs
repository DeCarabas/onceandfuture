namespace OnceAndFuture.DAL
{
    using OnceAndFuture.Syndication;
    using System;
    using System.Threading.Tasks;

    public class AggregateRiverStore : DocumentStore<string, River>
    {
        public AggregateRiverStore() : base(new BlobStore("onceandfuture", "aggregates"), "aggregates") { }

        protected override River GetDefaultValue(string id) =>
            new River(metadata: new RiverFeedMeta(originUrl: new Uri("aggregate:/" + id)));
        protected override string GetObjectID(string id) => id;
        public Task<River> LoadAggregate(string id) => GetDocument(id);
        public Task WriteAggregate(string id, River river) => WriteDocument(id, river);
    }
}
