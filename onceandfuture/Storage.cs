namespace OnceAndFuture
{
    using Newtonsoft.Json;
    using Polly;
    using Serilog;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    static class HeaderExtensions
    {
        public static string Value(this HttpHeaders headers, string header)
        {
            string result = "";
            if (headers.TryGetValues(header, out IEnumerable<string> headerValues))
            {
                result = String.Join(",", headerValues);
            }
            return result;
        }
    }

    public class BlobStore
    {
        static readonly HttpClient Client = CreateHttpClient();
        const string OriginalLengthHeader = "x-amz-meta-original-length";
        static string ValidPathCharacters = DetermineValidPathCharacters();

        static readonly Policy<HttpResponseMessage> HttpPolicy = Policy
            .Handle<HttpRequestException>(Policies.ShouldRetryException)
            .Or<TaskCanceledException>()
            .Or<WebException>(Policies.ShouldRetryException)
            .OrResult<HttpResponseMessage>(ShouldRetryFromResponse)
            .WaitAndRetryAsync(retryCount: 3, sleepDurationProvider: Policies.RetryTime);

        readonly string accessKeyId;
        readonly string bucket;
        readonly ILogger logger;
        readonly byte[] secretAccessKey;
        readonly string subdir;


        public BlobStore(string bucket, string subdir)
        {
            this.logger = Serilog.Log
                .ForContext(HoneycombSink.DatasetPropertyKey, "Storage")
                .ForContext("bucket", bucket)
                .ForContext("subdir", subdir);

            this.bucket = bucket;
            this.subdir = subdir;

            string accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            string secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

            if (String.IsNullOrWhiteSpace(accessKeyId) || String.IsNullOrWhiteSpace(secretAccessKey))
            {
                string credPath = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".aws", "credentials");
                if (File.Exists(credPath))
                {
                    string[] lines = File.ReadAllLines(credPath);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (line.StartsWith("aws_access_key_id", StringComparison.OrdinalIgnoreCase))
                        {
                            accessKeyId = line.Split(new[] { '=' }, 2)[1].Trim();
                        }
                        if (line.StartsWith("aws_secret_access_key", StringComparison.OrdinalIgnoreCase))
                        {
                            secretAccessKey = line.Split(new[] { '=' }, 2)[1].Trim();
                        }
                    }
                }
            }

            this.accessKeyId = accessKeyId;
            this.secretAccessKey = Encoding.UTF8.GetBytes(secretAccessKey);
        }

        static HttpClient CreateHttpClient()
        {
            const int TenMegabytes = 10 * 1024 * 1024;
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = false,
                UseDefaultCredentials = false,
                MaxConnectionsPerServer = 1000,
            };

            var client = new HttpClient(handler, true);
            client.Timeout = TimeSpan.FromSeconds(15);
            client.MaxResponseContentBufferSize = TenMegabytes;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TheOnceAndFuture/1.0");
            return client;
        }

        public Uri GetObjectUri(string name)
        {
            return new Uri(String.Concat(
                "https://s3-us-west-2.amazonaws.com/",
                this.bucket,
                "/",
                this.subdir,
                "/",
                Uri.EscapeDataString(name)
            ));
        }

        public Uri GetObjectUri(ObjectKey key)
        {
            string resourcePath = UrlEncode(this.bucket + "/" + key.ToString(), path: true);
            return new Uri("https://s3-us-west-2.amazonaws.com/" + resourcePath);
        }

        public async Task<byte[]> GetObject(string name)
        {
            ObjectKey key = KeyForName(name);
            byte[] result = await GetObject(key);
            if (result == null)
            {
                // In time all data will be moved to the new scheme, but until then...
                result = await GetObject(new ObjectKey(key: name));
            }
            return result;
        }

        public async Task<byte[]> GetObject(ObjectKey key)
        {
            Stopwatch timer = Stopwatch.StartNew();
            Func<HttpRequestMessage> request = () => new HttpRequestMessage
            {
                RequestUri = GetObjectUri(key),
                Method = HttpMethod.Get,
                Headers = { { "Date", DateTimeOffset.UtcNow.ToString("r") } }
            };

            string contentType = null;
            long? contentLength = null;
            long? objectLength = null;
            try
            {
                bool isCompressed;
                MemoryStream responseBuffer;
                using (HttpResponseMessage response = await SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        S3Error error = await DecodeError(response);
                        LogOperation("Get", key, null, null, null, timer, OperationStatus.Error, error.ResponseBody);
                        if (error.Code == "NoSuchKey")
                        {
                            return null;
                        }
                        if (error.Code == "AccessDenied")
                        {
                            return null;
                        }
                        throw new S3Exception("Get", key, error);
                    }

                    contentType = response.Content.Headers.ContentType.ToString();
                    contentLength = response.Content.Headers.ContentLength ?? 0;
                    objectLength = contentLength;
                    string ol = response.Headers.Value(OriginalLengthHeader);
                    if (!String.IsNullOrWhiteSpace(ol))
                    {
                        objectLength = long.Parse(ol);
                    }

                    responseBuffer = new MemoryStream();
                    await response.Content.CopyToAsync(responseBuffer);
                    responseBuffer.Position = 0;

                    isCompressed = response.Content.Headers.ContentEncoding.Contains("gzip");
                }

                byte[] data = new byte[objectLength.Value];
                if (isCompressed)
                {
                    var sourceStream = new GZipStream(responseBuffer, CompressionMode.Decompress);
                    var targetStream = new MemoryStream(data);
                    sourceStream.CopyTo(targetStream);
                }
                else
                {
                    responseBuffer.Read(data, 0, data.Length);
                }                

                LogOperation(
                    "Get", 
                    key, 
                    contentType, 
                    objectLength, 
                    contentLength, 
                    timer, 
                    OperationStatus.OK, 
                    null);
                return data;
            }
            catch (S3Exception) { throw; }
            catch (Exception e)
            {
                LogOperation(
                    "Get", 
                    key, 
                    contentType, 
                    objectLength, 
                    contentLength, 
                    timer, 
                    OperationStatus.Exception, 
                    e.ToString());
                throw;
            }
        }


        public async Task PutObject(string name, string type, MemoryStream stream, bool compress = false)
        {
            ObjectKey key = KeyForName(name);
            await PutObject(key, type, stream, compress);
        }

        public async Task PutObject(ObjectKey key, string type, MemoryStream stream, bool compress)
        {
            Stopwatch timer = Stopwatch.StartNew();
            long objectLength = stream.Length;
            long contentLength = objectLength;
            string encoding = null;
            Stream sourceStream = stream;

            try
            {
                if (compress)
                {
                    var tempStream = new MemoryStream();
                    using (var compressStream = new GZipStream(tempStream, CompressionMode.Compress, leaveOpen: true))
                    {
                        stream.CopyTo(compressStream);
                    }
                    tempStream.Position = 0;
                    sourceStream = tempStream;
                    encoding = "gzip";
                }
                contentLength = sourceStream.Length;

                Func<HttpRequestMessage> request = () => new HttpRequestMessage
                {
                    RequestUri = GetObjectUri(key),
                    Method = HttpMethod.Put,
                    Headers =
                    {
                        { "Date", DateTimeOffset.UtcNow.ToString("r") },
                        { OriginalLengthHeader, objectLength.ToString() },
                    },
                    Content = new StreamContent(sourceStream)
                    {
                        Headers =
                        {
                            { "Content-Type", type },
                            { "Content-Encoding", encoding },
                        },
                    },
                };

                using (HttpResponseMessage response = await SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        S3Error error = await DecodeError(response);
                        LogOperation(
                            "Put", 
                            key, 
                            type, 
                            objectLength, 
                            sourceStream.Length, 
                            timer, 
                            OperationStatus.Error, 
                            error.ResponseBody);
                        throw new S3Exception("Put", key, error);
                    }

                    LogOperation("Put", key, type, objectLength, contentLength, timer, OperationStatus.OK, null);
                }
            }
            catch (S3Exception) { throw; }
            catch (Exception e)
            {
                LogOperation(
                    "Put",
                    key,
                    type,
                    objectLength,
                    contentLength,
                    timer,
                    OperationStatus.Exception,
                    e.ToString());
                throw;
            }
        }

        /// <summary>Construct the Authorization header value for the specified request.</summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <remarks>See http://docs.aws.amazon.com/AmazonS3/latest/dev/RESTAuthentication.html#ConstructingTheAuthenticationHeader for more.</remarks>
        HttpRequestMessage AuthenticateRequest(HttpRequestMessage request)
        {
            string httpVerb = request.Method.ToString().ToUpperInvariant();
            string contentMD5 = request.Content?.Headers?.Value("Content-MD5") ?? String.Empty;
            string contentType = request.Content?.Headers?.Value("Content-Type") ?? String.Empty;
            string date = request.Headers.Value("Date");

            string canonicalizedAmzHeaders = "";
            foreach (KeyValuePair<string, IEnumerable<string>> headers in request.Headers)
            {
                if (headers.Key.StartsWith("x-amz-", StringComparison.OrdinalIgnoreCase))
                {
                    canonicalizedAmzHeaders += headers.Key.ToLowerInvariant() + ":" + String.Join(",", headers.Value) + "\n";
                }
            }

            string canonicalizedResource = request.RequestUri.AbsolutePath;

            string stringToSign =
                httpVerb + "\n" +
                contentMD5 + "\n" +
                contentType + "\n" +
                date + "\n" +
                canonicalizedAmzHeaders +
                canonicalizedResource;
            using (var hmac = new HMACSHA1(this.secretAccessKey))
            {
                string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
                request.Headers.Add("Authorization", "AWS " + accessKeyId + ":" + signature);
            }

            return request;
        }

        static async Task<S3Error> DecodeError(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync();
            XDocument doc = XDocument.Load(new StringReader(body));
            string code = doc.Root.Element("Code").Value;

            return new S3Error { Code = code, ResponseBody = body };
        }

        static string DetermineValidPathCharacters()
        {
            const string basePathCharacters = "/:'()!*[]$";

            var sb = new StringBuilder();
            foreach (var c in basePathCharacters)
            {
                var escaped = Uri.EscapeUriString(c.ToString());
                if (escaped.Length == 1 && escaped[0] == c)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        ObjectKey KeyForName(string name) => new ObjectKey(this.subdir, name);

        void LogOperation(
            string operation,
            ObjectKey key,
            string contentType,
            long? objectSize,
            long? storageSize,
            Stopwatch timer,
            OperationStatus status,
            string details
        )
        {
            this.logger.Information(
                "{Operation} {Key} ({ContentType} {ObjectBytes}bytes/{StorageBytes}compressed): {ElapsedMs}ms: {Status}: {Details}",
                operation, key.ToString(), contentType, objectSize, storageSize, timer.ElapsedMilliseconds, status.ToString(), details);
        }

        Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> request)
        {
            return HttpPolicy.ExecuteAsync(async () =>
            {
                HttpRequestMessage message = AuthenticateRequest(request());
                HttpResponseMessage response = await Client.SendAsync(message);
                if (!response.IsSuccessStatusCode) { await response.Content.LoadIntoBufferAsync(); }
                return response;
            });
        }

        static bool ShouldRetryFromResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) { return false; }
            S3Error error = DecodeError(response).Result;

            if (error.Code == "RequestTimeout") { return true; }

            return false;
        }

        static string UrlEncode(string data, bool path)
        {
            StringBuilder encoded = new StringBuilder(data.Length * 2);
            const string validUrlCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

            string unreservedChars = String.Concat(validUrlCharacters, (path ? ValidPathCharacters : ""));

            foreach (char symbol in System.Text.Encoding.UTF8.GetBytes(data))
            {
                if (unreservedChars.IndexOf(symbol) != -1)
                {
                    encoded.Append(symbol);
                }
                else
                {
                    encoded.Append("%").Append(string.Format(CultureInfo.InvariantCulture, "{0:X2}", (int)symbol));
                }
            }

            return encoded.ToString();
        }

        public struct ObjectKey
        {
            public string Key;

            public ObjectKey(string key) { Key = key; }

            public ObjectKey(string subdir, string name) { Key = subdir + "/" + name; }

            public override string ToString() => Key;
        }

        class S3Error
        {
            public string Code { get; set; }
            public string ResponseBody { get; set; }
        }

        class S3Exception : Exception
        {
            public S3Exception(string operation, ObjectKey key, S3Error error)
                : base(String.Format("An error occurred accessing S3 {0} {1}: {2}", operation, key, error.Code))
            {
                Operation = operation;
                Key = key;
                Error = error;
            }

            public string Operation { get; }
            public ObjectKey Key { get; }
            public S3Error Error { get; }
        }

        enum OperationStatus
        {
            OK,
            Error,
            Exception
        }
    }

    public abstract class DocumentStore<TDocumentID, TDocument>
    {
        readonly BlobStore blobStore;

        protected DocumentStore(BlobStore blobStore)
        {
            this.blobStore = blobStore;
        }

        protected abstract string GetObjectID(TDocumentID id);
        protected abstract TDocument GetDefaultValue(TDocumentID id);

        protected async Task<TDocument> GetDocument(TDocumentID docid)
        {
            string id = GetObjectID(docid);
            byte[] blob = await this.blobStore.GetObject(id);
            if (blob == null) { return GetDefaultValue(docid); }

            using (var memoryStream = new MemoryStream(blob))
            using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
            {
                string text = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<TDocument>(text, Policies.SerializerSettings);
            }
        }

        protected async Task WriteDocument(TDocumentID docid, TDocument document)
        {
            string id = GetObjectID(docid);
            byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(document, Policies.SerializerSettings));
            using (var memoryStream = new MemoryStream(data))
            {
                await this.blobStore.PutObject(id, "application/json", memoryStream, compress: true);
            }
        }
    }
}
