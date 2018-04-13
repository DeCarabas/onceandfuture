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

    public class RiverFeedMeta
    {
        public RiverFeedMeta(
            string name = null,
            Uri originUrl = null,
            string docs = null,
            string etag = null,
            DateTimeOffset? lastModified = null,
            HttpStatusCode? lastStatus = null,
            string owner = null,
            string mode = null,
            string next = null)
        {
            Name = name;
            OriginUrl = originUrl;
            Docs = docs ?? "http://riverjs.org/";
            Etag = etag;
            LastModified = lastModified;
            LastStatus = lastStatus ?? HttpStatusCode.OK;
            Owner = owner;
            Mode = mode;
            Next = next;
        }

        public RiverFeedMeta With(
            string name = null,
            Uri originUrl = null,
            string docs = null,
            string etag = null,
            DateTimeOffset? lastModified = null,
            HttpStatusCode? lastStatus = null,
            string owner = null,
            string mode = null,
            string next = null)
        {
            return new RiverFeedMeta(
                name ?? Name,
                originUrl ?? OriginUrl,
                docs ?? Docs,
                etag ?? Etag,
                lastModified ?? LastModified,
                lastStatus ?? LastStatus,
                owner ?? Owner,
                mode ?? Mode,
                next ?? Next);
        }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; }

        [JsonProperty(PropertyName = "originUrl")]
        public Uri OriginUrl { get; }

        [JsonProperty(PropertyName = "docs")]
        public string Docs { get; }

        [JsonProperty(PropertyName = "etag")]
        public string Etag { get; }

        [JsonProperty(PropertyName = "lastModified")]
        public DateTimeOffset? LastModified { get; }

        [JsonProperty(PropertyName = "lastStatus")]
        public HttpStatusCode LastStatus { get; }

        [JsonProperty(PropertyName = "owner")]
        public string Owner { get; }

        [JsonProperty(PropertyName = "mode")]
        public string Mode { get; }

        [JsonProperty(PropertyName = "next")]
        public string Next { get; }
    }

}
