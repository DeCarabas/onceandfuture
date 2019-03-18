namespace OnceAndFuture.Syndication
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Xml.Linq;

    public class Item
    {
        [JsonConstructor]
        public Item(
            string title = null,
            Uri link = null,
            string body = null,
            DateTime? pubDate = null,
            Uri permaLink = null,
            string comments = null,
            string id = null,
            Thumbnail thumbnail = null,
            IEnumerable<Enclosure> enclosures = null,
            XElement content = null,
            XElement description = null,
            XElement summary = null)
        {
            Title = title ?? String.Empty;
            Link = link;
            Body = body ?? String.Empty;
            PubDate = pubDate;
            PermaLink = permaLink;
            Comments = comments;
            Id = id;
            Thumbnail = thumbnail;
            Content = content;
            Description = description;
            Summary = summary;

            Enclosures =
                ImmutableList.CreateRange<Enclosure>(
                    enclosures ?? Enumerable.Empty<Enclosure>()
                );
        }

        public Item With(
            string title = null,
            Uri link = null,
            string body = null,
            DateTime? pubDate = null,
            Uri permaLink = null,
            string comments = null,
            string id = null,
            Thumbnail thumbnail = null,
            IEnumerable<Enclosure> enclosures = null,
            XElement content = null,
            XElement description = null,
            XElement summary = null)
        {
            return new Item(
                title ?? Title,
                link ?? Link,
                body ?? Body,
                pubDate ?? PubDate,
                permaLink ?? PermaLink,
                comments ?? Comments,
                id ?? Id,
                thumbnail ?? Thumbnail,
                enclosures ?? Enclosures,
                content ?? Content,
                description ?? Description,
                summary ?? Summary
            );
        }

        [JsonProperty(PropertyName = "title")]
        public string Title { get; }

        [JsonProperty(PropertyName = "link")]
        public Uri Link { get; }

        [JsonProperty(PropertyName = "body")]
        public string Body { get; }

        [JsonProperty(PropertyName = "pubDate")]
        public DateTime? PubDate { get; }

        [JsonProperty(PropertyName = "permaLink")]
        public Uri PermaLink { get; }

        [JsonProperty(PropertyName = "comments")]
        public string Comments { get; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; }

        [JsonProperty(PropertyName = "thumbnail")]
        public Thumbnail Thumbnail { get; }

        [JsonProperty(PropertyName = "enclosure")]
        public ImmutableList<Enclosure> Enclosures { get; }

        // Tracking elements during parsing; not stored or used afterwards.
        [JsonIgnore]
        public XElement Content { get; }

        [JsonIgnore]
        public XElement Description { get; }

        [JsonIgnore]
        public XElement Summary { get; }
    }
}
