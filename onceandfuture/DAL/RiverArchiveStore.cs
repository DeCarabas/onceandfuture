namespace OnceAndFuture.DAL
{
    using Newtonsoft.Json;
    using OnceAndFuture.Syndication;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    public class RiverArchiveStore
    {
        readonly BlobStore blobStore = new BlobStore("onceandfuture", "archives");

        public async Task<string> WriteRiverArchive(River oldRiver)
        {
            byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(oldRiver, Policies.SerializerSettings));
            string id = SyndicationUtil.HashBytes(data);
            using (var memoryStream = new MemoryStream(data))
            {
                await this.blobStore.PutObject(id, "application/json", memoryStream);
            }
            return id;
        }
    }
}
