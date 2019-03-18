namespace OnceAndFuture.DAL
{
    using OnceAndFuture.Syndication;
    using System;
    using System.Net;
    using System.Threading.Tasks;

    public class RiverFeedStore : DocumentStore<Uri, River>
    {
        public RiverFeedStore()
            : base(new BlobStore("onceandfuture", "feeds"), "feeds")
        { }

        protected override River GetDefaultValue(Uri id) =>
            new River(metadata: new RiverFeedMeta(originUrl: id));

        protected override string GetObjectID(Uri id) =>
            SyndicationUtil.HashString(id.AbsoluteUri);

        public async Task<River> LoadRiverForFeed(Uri feedUri)
        {
            River river = null;
            for (int i = 0; i < 30; i++)
            {
                river = await GetDocument(feedUri);
                if (river.Metadata.LastStatus != HttpStatusCode.Moved)
                {
                    break;
                }

                feedUri = river.Metadata.OriginUrl;
            }

            return river;
        }

        public Task WriteRiver(Uri uri, River river) =>
            WriteDocument(uri, river);
    }
}
