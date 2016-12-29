namespace onceandfuture
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Newtonsoft.Json;

    public class BlobStore
    {
        readonly string bucket;
        readonly AmazonS3Client client;

        // In deployment, use this?
        // Credentials stored in the AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables.
        public BlobStore(string bucket)
        {
            this.bucket = bucket;
            this.client = new AmazonS3Client(new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USWest2,
            });
        }

        public Uri GetObjectUri(string name)
        {
            return new Uri("https://s3-us-west-2.amazonaws.com/" + this.bucket + "/" + Uri.EscapeDataString(name));
        }

        public async Task<byte[]> GetObject(string name)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                using (GetObjectResponse response = await this.client.GetObjectAsync(this.bucket, name))
                {
                    byte[] data = new byte[response.ResponseStream.Length];
                    int cursor = 0;
                    while (cursor != data.Length)
                    {
                        int read = await response.ResponseStream.ReadAsync(data, cursor, data.Length - cursor);
                        if (read == 0) { break; }
                        cursor += read;
                    }
                    Log.GetObjectComplete(this.bucket, name, timer);
                    return data;
                }
            }
            catch (Amazon.S3.AmazonS3Exception s3e)
            {
                if (s3e.ErrorCode == "NoSuchKey")
                {
                    Log.GetObjectNotFound(this.bucket, name, timer);
                    return null;
                }
                Log.GetObjectError(this.bucket, name, s3e, timer, s3e.ErrorCode, s3e.ResponseBody);
                throw;
            }
        }

        public async Task PutObject(string name, string type, Stream stream)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                await this.client.PutObjectAsync(new PutObjectRequest
                {
                    AutoCloseStream = false,
                    BucketName = this.bucket,
                    Key = name,
                    ContentType = type,
                    InputStream = stream,
                });
                Log.PutObjectComplete(this.bucket, name, type, timer);
            }
            catch (AmazonS3Exception e)
            {
                Log.PutObjectError(this.bucket, name, type, e, timer, e.ErrorCode, e.ResponseBody);
                throw;
            }
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
                await this.blobStore.PutObject(id, "application/json", memoryStream);
            }
        }
    }
}
