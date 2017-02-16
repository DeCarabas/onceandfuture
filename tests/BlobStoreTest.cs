using Microsoft.VisualStudio.TestTools.UnitTesting;
using onceandfuture;
using System;
using System.IO;
using System.Threading.Tasks;

namespace tests
{
    [TestClass]
    public class BlobStoreTest
    {
        [TestMethod]
        public async Task BlobCompress()
        {            
            string name = "compress-" + Guid.NewGuid().ToString("n");
            byte[] firstObject = new byte[1024];
            var random = new Random();
            random.NextBytes(firstObject);

            var store = new BlobStore("onceandfuture-test", "test");
            await store.PutObject(name, "application/octet-stream", new MemoryStream(firstObject), compress: true);
            byte[] actualObject = await store.GetObject(name);

            CollectionAssert.AreEqual(firstObject, actualObject);
        }

        [TestMethod]
        public async Task BlobNoCompress()
        {
            string name = "nocompress-" + Guid.NewGuid().ToString("n");
            byte[] firstObject = new byte[1024];
            var random = new Random();
            random.NextBytes(firstObject);

            var store = new BlobStore("onceandfuture-test", "test");
            await store.PutObject(name, "application/octet-stream", new MemoryStream(firstObject), compress: false);
            byte[] actualObject = await store.GetObject(name);

            CollectionAssert.AreEqual(firstObject, actualObject);
        }

        [TestMethod]
        public async Task BlobReadFallback()
        {
            string name = "fallback-" + Guid.NewGuid().ToString("n");
            byte[] firstObject = new byte[1024];
            var random = new Random();
            random.NextBytes(firstObject);

            var store = new BlobStore("onceandfuture-test", "fallback");

            // Write with the old scheme...
            await store.PutObject(
                new BlobStore.ObjectKey(key: name),
                "application/octet-stream", 
                new MemoryStream(firstObject), 
                compress: true);

            // Should still be able to read.
            byte[] actualObject = await store.GetObject(name);

            CollectionAssert.AreEqual(firstObject, actualObject);
        }
    }
}
