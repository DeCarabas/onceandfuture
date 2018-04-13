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

    public class River
    {
        public River(UpdatedFeeds updatedFeeds = null, RiverFeedMeta metadata = null)
        {
            UpdatedFeeds = updatedFeeds ?? new UpdatedFeeds();
            Metadata = metadata ?? new RiverFeedMeta();
        }

        public River With(UpdatedFeeds updatedFeeds = null, RiverFeedMeta metadata = null)
        {
            return new River(updatedFeeds ?? UpdatedFeeds, metadata ?? Metadata);
        }

        [JsonProperty(PropertyName = "updatedFeeds")]
        public UpdatedFeeds UpdatedFeeds { get; }

        [JsonProperty(PropertyName = "metadata")]
        public RiverFeedMeta Metadata { get; }
    }
}
