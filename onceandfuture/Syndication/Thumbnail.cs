namespace OnceAndFuture.Syndication
{
    using Newtonsoft.Json;
    using System;

    public class Thumbnail
    {
        public Thumbnail(Uri url, int width, int height)
        {
            Url = url;
            Width = width;
            Height = height;
        }

        public Thumbnail With(Uri url = null, int? width = null, int? height = null)
        {
            return new Thumbnail(url ?? Url, width ?? Width, height ?? Height);
        }

        [JsonProperty(PropertyName = "url")]
        public Uri Url { get; }

        [JsonProperty(PropertyName = "width")]
        public int Width { get; }

        [JsonProperty(PropertyName = "height")]
        public int Height { get; }
    }
}
