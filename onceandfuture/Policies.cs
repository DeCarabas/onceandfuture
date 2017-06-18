namespace OnceAndFuture
{
    using Newtonsoft.Json;
    using Polly;
    using Polly.Retry;
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    static class Policies
    {
        public const int OneMegabyte = 1 * 1024 * 1024;
        public const int TenMegabytes = OneMegabyte * 10;

        // Decoding JPGs can be very memory-intensive; limit the amount of image data we attempt to decompress.
        public const int MaxImageFileSize = OneMegabyte;

        static ConcurrentDictionary<bool, HttpClient> clientCache = new ConcurrentDictionary<bool, HttpClient>();
        static Random random = new Random();

        public static readonly RetryPolicy HttpPolicy = Policy
            .Handle<HttpRequestException>(ShouldRetryException)
            .Or<TaskCanceledException>()
            .Or<WebException>(ShouldRetryException)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: RetryTime);

        public static readonly JsonSerializerSettings SerializerSettings =
            new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
                Formatting = Newtonsoft.Json.Formatting.None,
            };

        public static HttpClient CreateHttpClient(bool allowRedirect = true)
        {
            return clientCache.GetOrAdd(allowRedirect, (ar) =>
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = ar,
                    AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                    UseCookies = false,
                    UseDefaultCredentials = false,
                    MaxConnectionsPerServer = 1000,
                };

                var client = new HttpClient(handler, true);
                client.Timeout = TimeSpan.FromSeconds(15);
                client.MaxResponseContentBufferSize = TenMegabytes;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TheOnceAndFuture/1.0");
                return client;
            });
        }

        public static bool ImageResponseTooBig(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentLength == null)
            {
                return true;
            }

            if (response.Content.Headers.ContentLength.Value > MaxImageFileSize)
            {
                return true;
            }

            return false;
        }

        public static bool ShouldRetryException(HttpRequestException hre)
        {
            var iwe = hre.InnerException as WebException;
            if (iwe != null) { return ShouldRetryException(iwe); }

            return true;
        }

        public static bool ShouldRetryException(WebException iwe)
        {
            if (iwe.Message.Contains("The remote name could not be resolved")) { return false; }
            if (iwe.Message.Contains("The server committed a protocol violation")) { return false; }
            if (iwe.Message.Contains("SecureChannelFailure")) { return false; }
            return true;
        }

        public static TimeSpan RetryTime(int retryAttempt)
        {
            // var baseTime = TimeSpan.FromSeconds(Math.Pow(3, retryAttempt));
            var baseTime = TimeSpan.FromSeconds(5);

            // Add jitter, so that if a whole bunch of requests fail all at once the retries
            // are spaced out a little bit. This keeps us from repeatedly hammering at, say, 
            // an overloaded server.
            int jitterInterval = (int)(baseTime.TotalMilliseconds / 2.0);
            var jitter = TimeSpan.FromMilliseconds(random.Next(-jitterInterval, jitterInterval));

            return baseTime + jitter;
        }
    }
}
