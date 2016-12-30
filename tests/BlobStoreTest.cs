using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Runtime.Serialization;
using onceandfuture;
using System.IO;
using System.Threading.Tasks;

namespace tests
{
    [TestClass]
    public class BlobStoreTest
    {
        [TestMethod]
        public async Task TestCompress()
        {
            string name = "compress-" + Guid.NewGuid().ToString("n");
            byte[] firstObject = new byte[1024];
            var random = new Random();
            random.NextBytes(firstObject);

            var store = new BlobStore("onceandfuture-test");
            await store.PutObject(name, "application/octet-stream", new MemoryStream(firstObject), compress: true);
            byte[] actualObject = await store.GetObject(name);

            CollectionAssert.AreEqual(firstObject, actualObject);
        }

        [TestMethod]
        public async Task TestNoCompress()
        {
            string name = "nocompress-" + Guid.NewGuid().ToString("n");
            byte[] firstObject = new byte[1024];
            var random = new Random();
            random.NextBytes(firstObject);

            var store = new BlobStore("onceandfuture-test");
            await store.PutObject(name, "application/octet-stream", new MemoryStream(firstObject), compress: false);
            byte[] actualObject = await store.GetObject(name);

            CollectionAssert.AreEqual(firstObject, actualObject);
        }

    }
}
