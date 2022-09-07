using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}
