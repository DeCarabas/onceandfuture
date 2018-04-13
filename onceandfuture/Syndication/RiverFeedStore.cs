namespace OnceAndFuture.Syndication
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;

    public class RiverFeedStore : DocumentStore<Uri, River>
    {
        public RiverFeedStore() : base(new BlobStore("onceandfuture", "feeds"), "feeds") { }

        protected override River GetDefaultValue(Uri id) => new River(metadata: new RiverFeedMeta(originUrl: id));
        protected override string GetObjectID(Uri id) => SyndicationUtil.HashString(id.AbsoluteUri);
        public async Task<River> LoadRiverForFeed(Uri feedUri)
        {
            River river = null;
            for (int i = 0; i < 30; i++)
            {
                river = await GetDocument(feedUri);
                if (river.Metadata.LastStatus != HttpStatusCode.Moved) { break; }
                feedUri = river.Metadata.OriginUrl;
            }
            return river;
        }
        public Task WriteRiver(Uri uri, River river) => WriteDocument(uri, river);
    }


}
