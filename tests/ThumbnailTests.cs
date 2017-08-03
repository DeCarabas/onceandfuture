using Microsoft.VisualStudio.TestTools.UnitTesting;
using OnceAndFuture;
using System;
using System.IO;
using System.Threading.Tasks;

namespace tests
{
    [TestClass]
    public class ThumbnailTest
    {
        [TestMethod]
        public async Task TestThumbnail()
        {
            var service = new ThumbnailServiceClient();
            var response = await service.GetThumbnail(
                new Uri("https://aphyr.com/data/posts/347/2014-02-02_00049.jpg")
                );            
        }
    }
}