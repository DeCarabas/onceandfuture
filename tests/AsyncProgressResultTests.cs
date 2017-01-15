using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using onceandfuture;

namespace tests
{
    [TestClass]
    public class AsyncProgressResultTest
    {
        const int iterations = 1000;

        Random random;
        TaskCompletionSource<object>[] tasks;
        string[] descriptions;
        AsyncProgressActionResult actionResult;

        [TestInitialize]
        public void TestInitialize()
        {
            random = new Random();
            Reset();
        }

        [TestMethod]
        public void CompletedDescriptionsAreCorrect()
        {
            for (int i = 0; i < iterations; i++)
            {
                Reset();

                int notComplete = random.Next(tasks.Length);
                for (int taski = 0; taski < tasks.Length; taski++)
                {
                    if (taski == notComplete) { continue; }
                    tasks[taski].SetResult(new object());
                }

                Assert.AreEqual(descriptions[notComplete], actionResult.GetStatusMessage());

                tasks[notComplete].SetResult(new object());
                Assert.AreEqual("Done.", actionResult.GetStatusMessage());
            }
        }

        [TestMethod]
        public void NoCompletions()
        {
            for (int i = 0; i < iterations; i++)
            {
                Reset();

                Assert.AreEqual(descriptions[0], actionResult.GetStatusMessage());
                Assert.AreEqual(1, actionResult.GetCompletionPercent());
            }
        }

        [TestMethod]
        public void CompletionPercent()
        {
            Reset(count: 100);

            for (int i = 0; i < 100; i++)
            {
                tasks[i].SetResult(new object());
                Assert.AreEqual(i + 1, actionResult.GetCompletionPercent());
            }
        }

        [TestMethod]
        public void StillWaitingForFirst()
        {
            for (int i = 0; i < iterations; i++)
            {
                Reset();

                for (int taski = tasks.Length - 1; taski >= 1; taski--)
                {
                    tasks[taski].SetResult(new object());
                    Assert.AreEqual(descriptions[0], actionResult.GetStatusMessage());
                }
            }
        }

        void Reset(int? count = null)
        {
            int taskCount = count ?? random.Next(3, 300);
            tasks = new TaskCompletionSource<object>[taskCount];
            descriptions = new string[tasks.Length];
            for (int taski = 0; taski < tasks.Length; taski++)
            {
                tasks[taski] = new TaskCompletionSource<object>();
                descriptions[taski] = String.Format("Doing {0}...", taski);
            }

            actionResult = new AsyncProgressActionResult(tasks.Select(t => t.Task).ToArray(), descriptions);
        }
    }
}
