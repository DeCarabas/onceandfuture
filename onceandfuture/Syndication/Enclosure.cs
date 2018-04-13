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

    public class Enclosure
    {
        public Enclosure(Uri url, string type, string length)
        {
            Url = url;
            Type = type;
            Length = length;
        }

        public Enclosure With(Uri url = null, string type = null, string length = null)
        {
            return new Enclosure(url ?? Url, type ?? Type, length ?? Length);
        }

        [JsonProperty(PropertyName = "url")]
        public Uri Url { get; }

        [JsonProperty(PropertyName = "type")]
        public string Type { get; }

        [JsonProperty(PropertyName = "length")]
        public string Length { get; }
    }

}
