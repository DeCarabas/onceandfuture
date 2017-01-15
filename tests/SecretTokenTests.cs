using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using onceandfuture;

namespace tests
{
    [TestClass]
    public class SecretTokenTest
    {
        const int iterations = 1;

        [TestMethod]
        public void SecretTokensEqual()
        {
            for (int i = 0; i < iterations; i++)
            {
                SecretToken a = SecretToken.Create();
                AssertEqualInEveryWay(a, a);
            }
        }

        [TestMethod]
        public void SecretTokensNotEqual()
        {
            for (int i = 0; i < iterations; i++)
            {
                SecretToken a = SecretToken.Create();
                SecretToken b = SecretToken.Create();
                AssertNotEqualInAnyWay(a, b);
                AssertNotEqualInAnyWay(b, a);
            }
        }

        [TestMethod]
        public void SecretTokensNotEqualEmpty()
        {
            for (int i = 0; i < iterations; i++)
            {
                SecretToken a = SecretToken.Create();
                SecretToken b = SecretToken.Empty;
                AssertNotEqualInAnyWay(a, b);
                AssertNotEqualInAnyWay(b, a);
            }
        }

        [TestMethod]
        public void SecretTokensParse()
        {
            for (int i = 0; i < iterations; i++)
            {
                SecretToken a = SecretToken.Create();

                SecretToken b;
                Assert.IsTrue(SecretToken.TryParse(a.ToString(), out b));
                AssertEqualInEveryWay(a, b);
                AssertEqualInEveryWay(b, a);
            }
        }

        static void AssertEqualInEveryWay(SecretToken a, SecretToken b)
        {
            Assert.AreEqual(a, a);

#pragma warning disable 1718
            Assert.IsTrue(a == a);
            Assert.IsFalse(a != a);
#pragma warning restore 1718

            Assert.IsTrue(a.Equals(a));
            Assert.IsTrue(Object.Equals(a, a));
            Assert.AreEqual(a.GetHashCode(), a.GetHashCode());

            Assert.IsTrue(a.EqualsEncrypted(a.Encrypt()));
        }

        static void AssertNotEqualInAnyWay(SecretToken a, SecretToken b)
        {
            Assert.AreNotEqual(a, b);

            Assert.IsFalse(a == b);
            Assert.IsTrue(a != b);

            Assert.IsFalse(a.Equals(b));
            Assert.IsFalse(Object.Equals(a, b));
            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());

            Assert.IsFalse(b.EqualsEncrypted(a.Encrypt()));
        }
    }
}
