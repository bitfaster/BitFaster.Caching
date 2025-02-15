using System;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.UnitTests
{
    public class Threaded
    {
        public static Task Run(int threadCount, Action action)
        {
            return Run(threadCount, i => action());
        }

        public static async Task Run(int threadCount, Action<int> action)
        {
            var tasks = new Task[threadCount];
            ManualResetEvent mre = new ManualResetEvent(false);

            for (int i = 0; i < threadCount; i++)
            {
                int run = i; 
                tasks[i] = Task.Run(() =>
                {
                    mre.WaitOne();
                    action(run);
                });
            }

            mre.Set();

            await Task.WhenAll(tasks);
        }

        public static Task RunAsync(int threadCount, Func<Task> action)
        {
            return Run(threadCount, i => action());
        }

        public static async Task RunAsync(int threadCount, Func<int, Task> action)
        {
            var tasks = new Task[threadCount];
            ManualResetEvent mre = new ManualResetEvent(false);

            for (int i = 0; i < threadCount; i++)
            {
                int run = i;
                tasks[i] = Task.Run(async () =>
                {
                    mre.WaitOne();
                    await action(run);
                });
            }

            mre.Set();

            await Task.WhenAll(tasks);
        }
    }
}
