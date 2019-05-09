using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OnceAndFuture.Syndication;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OnceAndFuture
{
    static class ThumbnailLog
    {
        static readonly ILogger Logger = Serilog.Log.ForContext(HoneycombSink.DatasetPropertyKey, "Thumbnails");

        public static void LogThumbnail(
            Uri referrer,
            Uri thumbnailUri,
            string kind,
            ThumbnailResponse image,
            string judgement,
            Exception exception = null)
        {
            int? width = null;
            int? height = null;
            float? aspectRatio = null;
            float? area = null;

            if (image != null)
            {
                width = image.OriginalWidth;
                height = image.OriginalHeight;
                aspectRatio =
                    (float)Math.Max(image.OriginalWidth, image.OriginalHeight) /
                    (float)Math.Min(image.OriginalWidth, image.OriginalHeight);
                area = image.OriginalWidth * image.OriginalHeight;
            }

            Logger.Information(
                exception,
                "{Referrer}: Image {Thumbnail} ({Kind}): {Judgement} ({Width}x{Height} / Ratio: {AspectRatio} / Area: {Area})",
                referrer.AbsoluteUri, thumbnailUri.AbsoluteUri, kind, judgement, width, height, aspectRatio, area);
        }
    }

    public class ThumbnailRequest
    {
        [JsonProperty("dream")]
        public Uri Dream { get; set; }
        [JsonProperty("skipChecks")]
        public bool SkipChecks { get; set; }
        [JsonProperty("referrer")]
        public Uri Referrer { get; set; }
    }

    public class ThumbnailResponse
    {
        [JsonProperty("originalUrl")]
        public Uri OriginalUrl { get; set; }
        [JsonProperty("thumbnailUrl")]
        public Uri ThumbnailUrl { get; set; }
        [JsonProperty("originalWidth")]
        public int OriginalWidth { get; set; }
        [JsonProperty("originalHeight")]
        public int OriginalHeight { get; set; }
        [JsonProperty("thumbnailWidth")]
        public int ThumbnailWidth { get; set; }
        [JsonProperty("thumbnailHeight")]
        public int ThumbnailHeight { get; set; }
        [JsonProperty("error")]
        public JToken Error { get; set; }

        public string ErrorString()
        {
            if (Error == null) { return null; }
            if (Error.Type == JTokenType.String) { return Error.Value<string>(); }
            if (Error.Type == JTokenType.Object)
            {
                var errorObject = (JObject)Error;
                JToken value = errorObject["message"] ?? errorObject["msg"];
                if (value != null)
                {
                    if (value.Type == JTokenType.String) { return value.Value<string>(); }
                    return value.ToString();
                }
            }
            return Error.ToString();
        }
    }

    public class ThumbnailServiceClient
    {
        static readonly HttpClient client = Policies.CreateHttpClient();
        
        readonly string accessKeyId;
        readonly string secretAccessKey;

        public ThumbnailServiceClient()
        {
            AmazonUtils.GetAuthInfo(out this.accessKeyId, out this.secretAccessKey);
        }

        public async Task<ThumbnailResponse> GetThumbnail(
            Uri imageUri,
            bool skipChecks = false,
            Uri referrer = null
            )
        {
            var requestMessage = new HttpRequestMessage
            {
                RequestUri = new Uri("https://kuehe1it30.execute-api.us-west-2.amazonaws.com/prod/thumbr"),
                Method = HttpMethod.Post,
                Content = new StringContent(
                    JsonConvert.SerializeObject(new ThumbnailRequest
                    {
                        Dream = imageUri,
                        SkipChecks = skipChecks,
                        Referrer = referrer,
                    }, Policies.SerializerSettings),
                    System.Text.Encoding.UTF8,
                    "application/json"
                ),
                Headers = { { "Date", DateTimeOffset.UtcNow.ToString("r") } },
            };
            await AmazonUtils.AuthenticateRequestV4(
                requestMessage,
                "us-west-2",
                "execute-api",
                this.accessKeyId,
                this.secretAccessKey
                );

            using (HttpResponseMessage response = await client.SendAsync(requestMessage))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    return new ThumbnailResponse { OriginalUrl = imageUri, Error = JValue.CreateString(error) };
                }

                return JsonConvert.DeserializeObject<ThumbnailResponse>(
                    await response.Content.ReadAsStringAsync(),
                    Policies.SerializerSettings
                    );
            }
        }
    }

    class ThumbnailExtractor
    {
        readonly ThumbnailServiceClient thumbnailServiceClient = new ThumbnailServiceClient();

        static readonly HttpClient client = Policies.CreateHttpClient();
        static readonly MemoryCache imageCache;

        static readonly string[] BadThumbnails = new string[]
        {
            "addgoogle2.gif",
            "blank.jpg",
            "spacer.gif",
        };

        static readonly string[] BadThumbnailHosts = new string[]
        {
            "amazon-adsystem.com",
            "doubleclick.net",
            "googleadservices.com",
            "gravatar.com",
            "pixel.quantserve.com",
        };

        static ThumbnailExtractor()
        {
            imageCache = new MemoryCache(new MemoryCacheOptions());
        }

        public async Task<Item[]> LoadItemThumbnailsAsync(Uri baseUri, Item[] items)
        {
            Stopwatch loadTimer = Stopwatch.StartNew();
            Log.BeginLoadThumbnails(baseUri);
            Task<Item>[] itemTasks = new Task<Item>[items.Length];
            for (int i = 0; i < itemTasks.Length; i++)
            {
                itemTasks[i] = GetItemThumbnailAsync(baseUri, items[i]);
            }

            Item[] newItems = await Task.WhenAll(itemTasks);
            Log.EndLoadThumbnails(baseUri, newItems, loadTimer);
            return newItems;
        }

        static IHtmlDocument SoupFromElement(XElement element)
        {
            string htmlText = element.HasElements
                ? element.ToString(SaveOptions.DisableFormatting)
                : element.Value;
            return new HtmlParser().ParseDocument(htmlText);
        }

        async Task<Item> GetItemThumbnailAsync(Uri baseUri, Item item)
        {
            Uri itemLink = SyndicationUtil.Rebase(item.Link, baseUri);
            ThumbnailResponse sourceImage = null;

            // We might already have found a thumbnail...
            if (item.Thumbnail != null)
            {
                Uri baseUrl = itemLink ?? baseUri;
                Uri thumbnailUrl = MakeThumbnailUrl(baseUrl, item.Thumbnail.Url);
                if (thumbnailUrl != null)
                {
                    sourceImage = await FetchThumbnailAsync(
                        new ImageUrl { Kind = "EmbeddedThumb", Uri = thumbnailUrl },
                        baseUrl,
                        skipChecks: true);
                    if (sourceImage != null)
                    {
                        ThumbnailLog.LogThumbnail(baseUrl, thumbnailUrl, "EmbeddedThumb", sourceImage, "Best");
                    }
                }
            }

            // Look in the item soup; maybe we have it?
            string[] soup_ids = new string[] { "content", "description", "summary", };
            XElement[] soups = new XElement[] { item.Content, item.Description, item.Summary };
            for (int i = 0; i < soups.Length && sourceImage == null; i++)
            {
                XElement xe = soups[i];
                if (xe != null)
                {
                    Uri soupBase = SyndicationUtil.TryParseAbsoluteUrl(xe.BaseUri, baseUri) ?? itemLink ?? baseUri;
                    sourceImage = await FindThumbnailInSoupAsync(
                        soup_ids[i],
                        soupBase, 
                        SoupFromElement(soups[i])
                        );
                }
            }
            if (sourceImage == null && itemLink != null)
            {
                sourceImage = await FindImageAsync(itemLink);
            }

            if (sourceImage == null) { return item; }
            return item.With(
                thumbnail: new Thumbnail(
                    sourceImage.ThumbnailUrl,
                    sourceImage.ThumbnailWidth,
                    sourceImage.ThumbnailHeight
                    )
                );
        }

        public async Task<ThumbnailResponse> FindImageAsync(Uri uri)
        {
            try
            {
                HttpResponseMessage response = await Policies.HttpPolicy.ExecuteAsync(
                    _ => client.GetAsync(uri),
                    new Dictionary<string, object> { { "uri", uri } });
                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.FindThumbnailServerError(uri, response);
                        return null;
                    }

                    string mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                    if (mediaType.Contains("image"))
                    {
                        var iu = new ImageUrl { Uri = uri, Kind = "Direct" };
                        ThumbnailResponse result = await FetchThumbnailAsync(iu, uri, skipChecks: true);
                        if (result != null) { ThumbnailLog.LogThumbnail(uri, uri, iu.Kind, result, "Best"); }
                        return result;
                    }

                    if (mediaType.Contains("html"))
                    {
                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                        {
                            var parser = new HtmlParser();
                            IHtmlDocument document = await parser.ParseDocumentAsync(stream);

                            return await FindThumbnailInSoupAsync("item_document", uri, document);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Log.FindThumbnailTimeout(uri);
            }
            catch (HttpRequestException hre)
            {
                Log.FindThumbnailNetworkError(uri, hre);
            }
            catch (WebException we)
            {
                Log.FindThumbnailNetworkError(uri, we);
            }

            return null;
        }

        async Task<ThumbnailResponse> FindThumbnailInSoupAsync(string soup, Uri baseUrl, IHtmlDocument document)
        {
            // These get preferential treatment; if we find them then great otherwise we have to search the whole doc.
            // (Note that they also still have to pass the URL filter.)
            ImageUrl easyUri = SyndicationUtil.ConcatSequence(
                ExtractOpenGraphImageUrls(baseUrl, document),
                ExtractTwitterImageUrls(baseUrl, document),
                ExtractLinkRelImageUrls(baseUrl, document),
                ExtractKnownGoodnessImageUrls(baseUrl, document)
            ).FirstOrDefault();

            if (easyUri != null)
            {
                ThumbnailResponse easyResponse = await FetchThumbnailAsync(easyUri, baseUrl, skipChecks: true);
                ThumbnailLog.LogThumbnail(baseUrl, easyUri.Uri, easyUri.Kind, easyResponse, "Best");
                return easyResponse;
            }

            IEnumerable<Uri> distinctSrc =
                (from element in document.GetElementsByTagName("img")
                 let src = MakeThumbnailUrl(baseUrl, element.Attributes["src"]?.Value)
                 where src != null
                 select src).Distinct();

            ImageUrl[] imageUrls =
                (from src in distinctSrc
                 select new ImageUrl { Uri = src, Kind = "ImgTag" }).ToArray();

            Stopwatch loadTimer = Stopwatch.StartNew();
            Log.BeginGetThumbsFromSoup(soup, baseUrl, imageUrls.Length);
            var potentialThumbnails = new Task<ThumbnailResponse>[imageUrls.Length];
            for (int i = 0; i < potentialThumbnails.Length; i++)
            {
                potentialThumbnails[i] = FetchThumbnailAsync(imageUrls[i], baseUrl);
            }

            ThumbnailResponse[] images = await Task.WhenAll(potentialThumbnails);
            Log.EndGetThumbsFromSoup(soup, baseUrl, imageUrls.Length, loadTimer);

            ImageUrl bestImageUrl = null;
            ThumbnailResponse bestImage = null;
            float bestArea = 0;
            for (int i = 0; i < images.Length; i++)
            {
                ImageUrl imageUrl = imageUrls[i];
                ThumbnailResponse image = images[i];
                if (image == null) { continue; } // It was invalid.

                int width = image.OriginalWidth;
                int height = image.OriginalHeight;
                float area = width * height;
                if (area < 5000)
                {
                    ThumbnailLog.LogThumbnail(baseUrl, imageUrl.Uri, imageUrl.Kind, image, "Small");
                    CacheError(imageUrl, "Too Small");
                    continue;
                }

                float ratio = (float)Math.Max(width, height) / (float)Math.Min(width, height);
                if (ratio > 2.25f)
                {
                    ThumbnailLog.LogThumbnail(baseUrl, imageUrl.Uri, imageUrl.Kind, image, "Oblong");
                    CacheError(imageUrl, "Too Oblong");
                    continue;
                }

                if (imageUrl.Uri.AbsolutePath.Contains("sprite")) { area /= 10; } // Penalize images named "sprite"

                if (area > bestArea)
                {
                    if (bestImageUrl != null)
                    {
                        CacheError(bestImageUrl, "Not the best");
                        ThumbnailLog.LogThumbnail(baseUrl, bestImageUrl.Uri, bestImageUrl.Kind, bestImage, "NotBest");
                    }

                    bestArea = area;
                    bestImage = image;
                    bestImageUrl = imageUrls[i];
                }
                else
                {
                    ThumbnailLog.LogThumbnail(baseUrl, imageUrl.Uri, imageUrl.Kind, image, "NotBest");
                }
            }

            if (bestImage != null)
            {
                ThumbnailLog.LogThumbnail(baseUrl, bestImageUrl.Uri, bestImageUrl.Kind, bestImage, "Best");
                Log.FoundThumbnail(soup, baseUrl, bestImageUrl.Uri, bestImageUrl.Kind);
            }
            else
            {
                Log.NoThumbnailFound(soup, baseUrl);
            }
            return bestImage;
        }

        static IEnumerable<ImageUrl> ExtractKnownGoodnessImageUrls(Uri baseUrl, IHtmlDocument document)
        {
            IElement element = document.QuerySelector("section.comic-art");
            if (element != null)
            {
                Uri uri = MakeThumbnailUrl(baseUrl, element.QuerySelector("img")?.GetAttribute("src"));
                if (uri != null) { yield return new ImageUrl { Uri = uri, Kind = "KnownGood" }; }
            }
        }

        static IEnumerable<ImageUrl> ExtractLinkRelImageUrls(Uri baseUrl, IHtmlDocument document)
        {
            return
                from element in document.All
                where element.LocalName == "link"
                where element.Attributes["rel"]?.Value == "image_src"
                let thumbnail = MakeThumbnailUrl(baseUrl, element.Attributes["href"]?.Value)
                where thumbnail != null
                select new ImageUrl { Uri = thumbnail, Kind = "RelImage" };
        }

        static IEnumerable<ImageUrl> ExtractTwitterImageUrls(Uri baseUrl, IHtmlDocument document)
        {
            return
                from element in document.All
                where element.LocalName == "meta"
                where
                    element.Attributes["name"]?.Value == "twitter:image" ||
                    element.Attributes["property"]?.Value == "twitter:image"
                let thumbnail = MakeThumbnailUrl(baseUrl, element.Attributes["content"]?.Value)
                where thumbnail != null
                select new ImageUrl { Uri = thumbnail, Kind = "TwitterImage" };
        }

        static IEnumerable<ImageUrl> ExtractOpenGraphImageUrls(Uri baseUrl, IHtmlDocument document)
        {
            return
                from element in document.All
                where element.LocalName == "meta"
                where
                    element.Attributes["name"]?.Value == "og:image" ||
                    element.Attributes["property"]?.Value == "og:image" ||
                    element.Attributes["name"]?.Value == "og:image:url" ||
                    element.Attributes["property"]?.Value == "og:image:url"
                let thumbnail = MakeThumbnailUrl(baseUrl, element.Attributes["content"]?.Value)
                where thumbnail != null
                select new ImageUrl { Uri = thumbnail, Kind = "OpenGraph" };
        }

        static readonly TimeSpan ErrorCacheLifetime = TimeSpan.FromSeconds(30);
        static readonly TimeSpan SuccessCacheLifetime = TimeSpan.FromHours(1);

        async Task<ThumbnailResponse> FetchThumbnailAsync(ImageUrl imageUrl, Uri referrer, bool skipChecks = false)
        {
            try
            {
                // N.B.: We put the whole bit of cache logic in here because somebody might succeed or fail altogether
                //       while we wait on retries, and we want to re-check the cache on every loop.
                return await Policies.HttpPolicy.ExecuteAsync(async _ =>
                {
                    object cachedObject = imageCache.Get(imageUrl.Uri.AbsoluteUri);
                    if (cachedObject is string)
                    {
                        Log.ThumbnailErrorCacheHit(referrer, imageUrl.Uri, cachedObject);
                        return null;
                    }
                    if (cachedObject is ThumbnailResponse)
                    {
                        Log.ThumbnailSuccessCacheHit(referrer, imageUrl.Uri);
                        return (ThumbnailResponse)cachedObject;
                    }

                    ThumbnailResponse response = await this.thumbnailServiceClient.GetThumbnail(
                        imageUrl.Uri,
                        skipChecks: skipChecks,
                        referrer: referrer
                        );
                    if (response.Error != null)
                    {
                        ThumbnailLog.LogThumbnail(referrer, imageUrl.Uri, imageUrl.Kind, null, response.ErrorString());
                        CacheError(imageUrl, response.ErrorString());
                        return null;
                    }

                    return CacheSuccess(imageUrl, response);
                },
                new Dictionary<string, object> { { "uri", imageUrl.Uri } });
            }
            catch (TaskCanceledException tce)
            {
                ThumbnailLog.LogThumbnail(referrer, imageUrl.Uri, imageUrl.Kind, null, "Timeout");
                CacheError(imageUrl, tce.Message);
                return null;
            }
            catch (HttpRequestException hre)
            {
                ThumbnailLog.LogThumbnail(referrer, imageUrl.Uri, imageUrl.Kind, null, "NetworkError", hre);
                CacheError(imageUrl, hre.Message);
                return null;
            }
            catch (WebException we)
            {
                ThumbnailLog.LogThumbnail(referrer, imageUrl.Uri, imageUrl.Kind, null, "NetworkError", we);
                CacheError(imageUrl, we.Message);
                return null;
            }
        }

        static ThumbnailResponse CacheSuccess(ImageUrl imageUrl, ThumbnailResponse image)
        {
            return imageCache.Set(imageUrl.Uri.AbsoluteUri, image, SuccessCacheLifetime);
        }

        static void CacheError(ImageUrl imageUrl, string message)
        {
            imageCache.Set(imageUrl.Uri.AbsoluteUri, message, ErrorCacheLifetime);
        }

        static Uri MakeThumbnailUrl(Uri baseUrl, string src)
        {
            Uri thumbnail = SyndicationUtil.TryParseAbsoluteUrl(src, baseUrl);
            if (thumbnail == null) { return null; }
            return MakeThumbnailUrl(baseUrl, thumbnail);
        }

        static Uri MakeThumbnailUrl(Uri baseUrl, Uri thumbnail)
        {
            thumbnail = SyndicationUtil.Rebase(thumbnail, baseUrl);
            if (!thumbnail.IsAbsoluteUri) { return null; }
            if (thumbnail.Scheme != "http" && thumbnail.Scheme != "https")
            {
                return null;
            }

            for (int i = 0; i < BadThumbnails.Length; i++)
            {
                if (thumbnail.AbsolutePath.EndsWith(BadThumbnails[i], StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            for (int i = 0; i < BadThumbnailHosts.Length; i++)
            {
                if (thumbnail.Host.EndsWith(BadThumbnailHosts[i], StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return thumbnail;
        }

        class ImageUrl
        {
            public string Kind;
            public Uri Uri;
        }
    }
}
