using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using ImageSharp;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
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
            Image<Rgba32> image,
            string judgement,
            Exception exception = null)
        {
            int? width = null;
            int? height = null;
            float? aspectRatio = null;
            float? area = null;

            if (image != null)
            {
                width = image.Width;
                height = image.Height;
                aspectRatio = (float)Math.Max(image.Width, image.Height) / (float)Math.Min(image.Width, image.Height);
                area = image.Width * image.Height;
            }

            Logger.Information(
                exception,
                "{Referrer}: Image {Thumbnail} ({Kind}): {Judgement} ({Width}x{Height} / Ratio: {AspectRatio} / Area: {Area})",
                referrer.AbsoluteUri, thumbnailUri.AbsoluteUri, kind, judgement, width, height, aspectRatio, area);
        }
    }

    static class EntropyCropper
    {
        const int MaximumSlice = 10;

        static byte[] ToGreyscale(Image<Rgba32> image)
        {
            Span<Rgba32> pixels = new Image<Rgba32>(image).Grayscale().Pixels;
            byte[] bytes = new byte[pixels.Length];
            for (int i = 0; i < bytes.Length; i++) { bytes[i] = pixels[i].R; }
            return bytes;
        }

        static double Entropy(byte[] pix, int stride, int[] hist, int left, int top, int right, int bottom)
        {
            Array.Clear(hist, 0, hist.Length);
            for (int iy = top; iy < bottom; iy++)
            {
                int idx = (iy * stride) + left;
                for (int ix = left; ix < right; ix++)
                {
                    hist[pix[idx]]++;
                    idx++;
                }
            }

            double sum = 0;

            // In the math this is sum(hist), but that's weird because it's really just the area of the bitmap.
            double area = (right - left) * (bottom - top);
            for (int i = 0; i < hist.Length; i++)
            {
                if (hist[i] != 0)
                {
                    double v = ((double)hist[i]) / area;
                    sum += v * Math.Log(v, 2.0);
                }
            }
            return -sum;
        }

        static void CropVertical(byte[] pix, int width, int height, int targetHeight, out int top, out int bottom)
        {
            int[] hist = new int[256];
            top = 0;
            bottom = height;
            while (bottom - top > targetHeight)
            {
                int sliceHeight = Math.Min((bottom - top) - targetHeight, MaximumSlice);

                double topEntropy = Entropy(
                    pix, width, hist,
                    0, top,
                    width, top + sliceHeight);

                double bottomEntropy = Entropy(
                    pix, width, hist,
                    0, bottom - sliceHeight,
                    width, bottom);
                if (topEntropy < bottomEntropy)
                {
                    // Top has less entropy, cut it by moving top down.
                    top += sliceHeight;
                }
                else
                {
                    // Bottom has less entropy, cut it by moving bottom up.
                    bottom -= sliceHeight;
                }
            }
        }

        static void CropHorizontal(byte[] pix, int width, int height, int targetWidth, out int left, out int right)
        {
            int[] hist = new int[256];
            left = 0;
            right = width;
            while (right - left > targetWidth)
            {
                int sliceWidth = Math.Min((right - left) - targetWidth, MaximumSlice);

                double leftEntropy = Entropy(
                    pix, width, hist,
                    left, 0,
                    left + sliceWidth, height);

                double rightEntropy = Entropy(
                    pix, width, hist,
                    right - sliceWidth, 0,
                    right, height);
                if (leftEntropy < rightEntropy)
                {
                    // Left has less entropy, cut it by moving left.
                    left += sliceWidth;
                }
                else
                {
                    // Right has less entropy, cut it by moving right.
                    right -= sliceWidth;
                }
            }
        }

        static void CropSquare(
            byte[] pix, int width, int height, out int left, out int top, out int right, out int bottom)
        {
            if (width > height)
            {
                top = 0; bottom = height;
                CropHorizontal(pix, width, height, height, out left, out right);
            }
            else
            {
                left = 0; right = width;
                CropVertical(pix, width, height, width, out top, out bottom);
            }
        }

        public static Image<Rgba32> Crop(Image<Rgba32> image, int targetSize)
        {
            byte[] values = ToGreyscale(image);
            int width = image.Width;
            int height = image.Height;

            int left, right, top, bottom;
            CropSquare(values, width, height, out left, out top, out right, out bottom);

            // Don't enlarge, it adds nothing.
            int sourceWidth = right - left;
            if (sourceWidth < targetSize) { targetSize = sourceWidth; }

            return image
                .Crop(new Rectangle(left, top, right - left, bottom - top))
                .Resize(targetSize, targetSize);
        }
    }

    static class ThumbnailGate
    {
        // ImageSharp consumes tons of resources, and so we want to gate the amount of concurrent JPEG decoding we do.
        // Right now we constrain to 1; let's see if that helps anything.
        const int MaximumConcurrentLoads = 1;

        static readonly SemaphoreSlim loadGate = new SemaphoreSlim(MaximumConcurrentLoads);

        public static async Task<IDisposable> Enter()
        {
            await loadGate.WaitAsync();
            return new SemaphoreLock();
        }

        class SemaphoreLock : IDisposable
        {
            public void Dispose() => loadGate.Release();
        }
    }

    class ThumbnailExtractor
    {
        const int ThumbnailSize = 312;

        readonly RiverThumbnailStore thumbnailStore = new RiverThumbnailStore();

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
            imageCache = new MemoryCache(new MemoryCacheOptions
            {
                CompactOnMemoryPressure = true,
            });
        }

        public static void ConfigureProcess()
        {
            var formats = new ImageSharp.Formats.IImageFormat[]
            {
                    new ImageSharp.Formats.BmpFormat(),
                    new ImageSharp.Formats.PngFormat(),
                    new ImageSharp.Formats.JpegFormat(),
                    new ImageSharp.Formats.GifFormat(),
            };
            foreach (var fmt in formats) { ImageSharp.Configuration.Default.AddImageFormat(fmt); }
        }

        public async Task<RiverItem[]> LoadItemThumbnailsAsync(Uri baseUri, RiverItem[] items)
        {
            Stopwatch loadTimer = Stopwatch.StartNew();
            Log.BeginLoadThumbnails(baseUri);
            Task<RiverItem>[] itemTasks = new Task<RiverItem>[items.Length];
            for (int i = 0; i < itemTasks.Length; i++)
            {
                itemTasks[i] = GetItemThumbnailAsync(baseUri, items[i]);
            }

            RiverItem[] newItems = await Task.WhenAll(itemTasks);
            Log.EndLoadThumbnails(baseUri, newItems, loadTimer);
            return newItems;
        }

        static IHtmlDocument SoupFromElement(XElement element)
        {
            string htmlText = element.HasElements
                ? element.ToString(SaveOptions.DisableFormatting)
                : element.Value;
            return new HtmlParser().Parse(htmlText);
        }

        async Task<RiverItem> GetItemThumbnailAsync(Uri baseUri, RiverItem item)
        {
            Uri itemLink = Util.Rebase(item.Link, baseUri);
            Image<Rgba32> sourceImage = null;

            // We might already have found a thumbnail...
            if (item.Thumbnail != null)
            {
                Uri baseUrl = itemLink ?? baseUri;
                Uri thumbnailUrl = MakeThumbnailUrl(baseUrl, item.Thumbnail.Url);
                if (thumbnailUrl != null)
                {
                    sourceImage = await FetchThumbnailAsync(
                        new ImageUrl { Kind = "EmbeddedThumb", Uri = thumbnailUrl },
                        baseUrl);
                    if (sourceImage != null)
                    {
                        ThumbnailLog.LogThumbnail(baseUrl, thumbnailUrl, "EmbeddedThumb", sourceImage, "Best");
                    }
                }
            }

            // Look in the item soup; maybe we have it?
            XElement[] soups = new XElement[] { item.Content, item.Description, item.Summary };
            for (int i = 0; i < soups.Length && sourceImage == null; i++)
            {
                XElement xe = soups[i];
                if (xe != null)
                {
                    Uri soupBase = Util.TryParseAbsoluteUrl(xe.BaseUri, baseUri) ?? itemLink ?? baseUri;
                    sourceImage = await FindThumbnailInSoupAsync(soupBase, SoupFromElement(soups[i]));
                }
            }
            if (sourceImage == null && itemLink != null)
            {
                sourceImage = await FindImageAsync(itemLink);
            }

            if (sourceImage == null) { return item; }
            Image<Rgba32> thumbnail = MakeThumbnail(sourceImage);

            Uri thumbnailUri = await this.thumbnailStore.StoreImage(thumbnail);
            return item.With(
                thumbnail: new RiverItemThumbnail(thumbnailUri, thumbnail.Width, thumbnail.Height));
        }

        public static Image<Rgba32> MakeThumbnail(Image<Rgba32> sourceImage)
        {
            return EntropyCropper.Crop(sourceImage, ThumbnailSize);
        }

        public static async Task<Image<Rgba32>> FindImageAsync(Uri uri)
        {
            try
            {
                HttpResponseMessage response = await Policies.HttpPolicy.ExecuteAsync(
                    () => client.GetAsync(uri),
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
                        Image<Rgba32> result = await FetchThumbnailAsync(iu, uri);
                        if (result != null) { ThumbnailLog.LogThumbnail(uri, uri, iu.Kind, result, "Best"); }
                        return result;
                    }

                    if (mediaType.Contains("html"))
                    {
                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                        {
                            var parser = new HtmlParser();
                            IHtmlDocument document = await parser.ParseAsync(stream);

                            return await FindThumbnailInSoupAsync(uri, document);
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

        static async Task<Image<Rgba32>> FindThumbnailInSoupAsync(Uri baseUrl, IHtmlDocument document)
        {
            // These get preferential treatment; if we find them then great otherwise we have to search the whole doc.
            // (Note that they also still have to pass the URL filter.)
            ImageUrl easyUri = Util.ConcatSequence(
                ExtractOpenGraphImageUrls(baseUrl, document),
                ExtractTwitterImageUrls(baseUrl, document),
                ExtractLinkRelImageUrls(baseUrl, document),
                ExtractKnownGoodnessImageUrls(baseUrl, document)
            ).FirstOrDefault();

            if (easyUri != null)
            {
                ThumbnailLog.LogThumbnail(baseUrl, easyUri.Uri, easyUri.Kind, null, "Best");
                return await FetchThumbnailAsync(easyUri, baseUrl);
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
            Log.BeginGetThumbsFromSoup(baseUrl, imageUrls.Length);
            var potentialThumbnails = new Task<Image<Rgba32>>[imageUrls.Length];
            for (int i = 0; i < potentialThumbnails.Length; i++)
            {
                potentialThumbnails[i] = FetchThumbnailAsync(imageUrls[i], baseUrl);
            }

            Image<Rgba32>[] images = await Task.WhenAll(potentialThumbnails);
            Log.EndGetThumbsFromSoup(baseUrl, imageUrls.Length, loadTimer);

            ImageUrl bestImageUrl = null;
            Image<Rgba32> bestImage = null;
            int bestArea = 0;
            for (int i = 0; i < images.Length; i++)
            {
                ImageUrl imageUrl = imageUrls[i];
                Image<Rgba32> image = images[i];
                if (image == null) { continue; } // It was invalid.

                int width = image.Width;
                int height = image.Height;
                int area = width * height;
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

                if (imageUrl.Uri.AbsolutePath.Contains("sprite")) { ratio /= 10; } // Penalize images named "sprite"

                if (ratio > bestArea)
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
                Log.FoundThumbnail(baseUrl, bestImageUrl.Uri, bestImageUrl.Kind);
            }
            else
            {
                Log.NoThumbnailFound(baseUrl);
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

        static async Task<Image<Rgba32>> FetchThumbnailAsync(ImageUrl imageUrl, Uri referrer)
        {
            try
            {
                // N.B.: We put the whole bit of cache logic in here because somebody might succeed or fail altogether
                //       while we wait on retries, and we want to re-check the cache on every loop.
                return await Policies.HttpPolicy.ExecuteAsync(async () =>
                {
                    object cachedObject = imageCache.Get(imageUrl.Uri.AbsoluteUri);
                    if (cachedObject is string)
                    {
                        Log.ThumbnailErrorCacheHit(referrer, imageUrl.Uri, cachedObject);
                        return null;
                    }
                    if (cachedObject is Image<Rgba32>)
                    {
                        Log.ThumbnailSuccessCacheHit(referrer, imageUrl.Uri);
                        return (Image<Rgba32>)cachedObject;
                    }

                    var request = new HttpRequestMessage(HttpMethod.Get, imageUrl.Uri);
                    if (referrer != null) { request.Headers.Referrer = referrer; }

                    HttpResponseMessage response = await client.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        ThumbnailLog.LogThumbnail(referrer, imageUrl.Uri, imageUrl.Kind, null, response.ReasonPhrase);
                        CacheError(imageUrl, response.ReasonPhrase);
                        return null;
                    }

                    if (Policies.ImageResponseTooBig(response))
                    {
                        ThumbnailLog.LogThumbnail(referrer, imageUrl.Uri, imageUrl.Kind, null, "ImageTooBig");
                        CacheError(imageUrl, "ImageTooBig");
                        return null;
                    }

                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                    try
                    {
                        // TODO: Record the original image and the result of loading somewhere.
                        using (await ThumbnailGate.Enter())
                        {
                            Image<Rgba32> streamImage = Image.Load(imageBytes);
                            return CacheSuccess(imageUrl, streamImage);
                        }
                    }
                    catch (Exception ae)
                    {
                        ThumbnailLog.LogThumbnail(referrer, imageUrl.Uri, imageUrl.Kind, null, "LoadException");
                        CacheError(imageUrl, ae.Message);
                        return null;
                    }
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

        static Image<Rgba32> CacheSuccess(ImageUrl imageUrl, Image<Rgba32> image)
        {
            // Bypass the cache if the image is too big.
            if (image.Width * image.Height >= 5000) { return image; }

            return imageCache.Set(imageUrl.Uri.AbsoluteUri, image, SuccessCacheLifetime);
        }

        static void CacheError(ImageUrl imageUrl, string message)
        {
            imageCache.Set(imageUrl.Uri.AbsoluteUri, message, ErrorCacheLifetime);
        }

        static Uri MakeThumbnailUrl(Uri baseUrl, string src)
        {
            Uri thumbnail = Util.TryParseAbsoluteUrl(src, baseUrl);
            if (thumbnail == null) { return null; }
            return MakeThumbnailUrl(baseUrl, thumbnail);
        }

        static Uri MakeThumbnailUrl(Uri baseUrl, Uri thumbnail)
        {
            thumbnail = Util.Rebase(thumbnail, baseUrl);
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

    public class RiverThumbnailStore
    {
        readonly BlobStore blobStore = new BlobStore("onceandfuture-thumbs", "thumbs");

        static bool IsTransparent(Image<Rgba32> image)
        {
            Span<Rgba32> pixels = image.Pixels;
            for (int i = 0; i < pixels.Length; i++)
            {
                Rgba32 pixel = pixels[i];
                if (pixel.A != 255) { return true; }
            }
            return false;
        }

        public async Task<Uri> StoreImage(Image<Rgba32> image)
        {
            MemoryStream stream = new MemoryStream();
            string extension;
            string mimeType;

            if (IsTransparent(image))
            {
                mimeType = "image/png";
                extension = ".png";
                image.SaveAsPng(stream);
            }
            else
            {
                mimeType = "image/jpeg";
                extension = ".jpg";
                image.SaveAsJpeg(stream);
            }

            stream.Position = 0;
            byte[] hash = SHA1.Create().ComputeHash(stream);
            string fileName = Convert.ToBase64String(hash).Replace('/', '-') + extension;

            stream.Position = 0;
            await this.blobStore.PutObject(fileName, mimeType, stream);
            return this.blobStore.GetObjectUri(fileName);
        }
    }
}
