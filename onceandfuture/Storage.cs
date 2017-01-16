namespace onceandfuture
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Newtonsoft.Json;

    public class BlobStore
    {
        const string OriginalLength = "x-amz-meta-original-length";

        readonly string bucket;
        readonly AmazonS3Client client;
        readonly string subdir;

        // In deployment, use this?
        // Credentials stored in the AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables.
        public BlobStore(string bucket, string subdir)
        {
            this.bucket = bucket;
            this.subdir = subdir;
            this.client = new AmazonS3Client(new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USWest2,
            });
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

        public async Task<byte[]> GetObject(string name)
        {
            ObjectKey key = KeyForName(name);
            byte[] result = await GetObjectInternal(key);
            if (result == null)
            {
                // In time all data will be moved to the new scheme, but until then...
                result = await GetObjectInternal(new ObjectKey(key: name));
            }
            return result;
        }

        async Task<byte[]> GetObjectInternal(ObjectKey key)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                using (GetObjectResponse response = await this.client.GetObjectAsync(this.bucket, key.Key))
                {
                    long length = response.ResponseStream.Length;
                    string ol = response.Metadata[OriginalLength];
                    if (!String.IsNullOrWhiteSpace(ol))
                    {
                        length = long.Parse(ol);
                    }

                    byte[] data = new byte[length];
                    if (response.Headers.ContentEncoding == "gzip")
                    {
                        var targetStream = new MemoryStream(data);
                        var sourceStream = new GZipStream(response.ResponseStream, CompressionMode.Decompress);
                        await sourceStream.CopyToAsync(targetStream);
                    }
                    else
                    {
                        int cursor = 0;
                        while (cursor != data.Length)
                        {
                            int read = await response.ResponseStream.ReadAsync(data, cursor, data.Length - cursor);
                            if (read == 0) { break; }
                            cursor += read;
                        }
                    }

                    Log.GetObjectComplete(this.bucket, key.Key, timer);
                    return data;
                }
            }
            catch (Amazon.S3.AmazonS3Exception s3e)
            {
                if (s3e.ErrorCode == "NoSuchKey")
                {
                    Log.GetObjectNotFound(this.bucket, key.Key, timer);
                    return null;
                }
                if (s3e.ErrorCode == "AccessDenied")
                {
                    Log.GetObjectAccessDenied(this.bucket, key.Key, timer);
                    return null;
                }
                Log.GetObjectError(this.bucket, key.Key, s3e, timer, s3e.ErrorCode, s3e.ResponseBody);
                throw;
            }
        }

        public async Task PutObject(string name, string type, MemoryStream stream, bool compress = false)
        {
            ObjectKey key = KeyForName(name);
            await PutObjectInternal(key, type, stream, compress);
        }

        public async Task PutObjectInternal(ObjectKey key, string type, MemoryStream stream, bool compress)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                string encoding = null;
                Stream sourceStream = stream;
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

                var request = new PutObjectRequest
                {
                    AutoCloseStream = false,
                    BucketName = this.bucket,
                    Key = key.Key,
                    ContentType = type,
                    InputStream = sourceStream,
                };
                request.Headers.ContentEncoding = encoding;
                request.Metadata.Add(OriginalLength, stream.Length.ToString());

                await this.client.PutObjectAsync(request);
                Log.PutObjectComplete(this.bucket, key.Key, type, timer, stream);
            }
            catch (AmazonS3Exception e)
            {
                Log.PutObjectError(this.bucket, key.Key, type, e, timer, e.ErrorCode, e.ResponseBody);
                throw;
            }
        }

        ObjectKey KeyForName(string name) => new ObjectKey(this.subdir, name);

        public struct ObjectKey
        {
            public string Key;

            public ObjectKey(string key) { Key = key; }

            public ObjectKey(string subdir, string name) { Key = subdir + "/" + name; }

            public override string ToString() => Key;
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
