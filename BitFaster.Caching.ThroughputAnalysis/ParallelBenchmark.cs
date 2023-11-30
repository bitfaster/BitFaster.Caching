using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class ParallelBenchmark
    {
        public static TimeSpan Run(Action<int, IThroughputBenchConfig, ICache<long, int>> action, int threads, IThroughputBenchConfig config, ICache<long, int> cache)
        {
            Task[] tasks = new Task[threads];
            ManualResetEventSlim mre = new ManualResetEventSlim();

            Action<int, IThroughputBenchConfig, ICache<long, int>> syncStart = (taskId, config, cache) =>
            {
                mre.Wait();
                action(taskId, config, cache);
            };

            for (int i = 0; i < tasks.Length; i++)
            {
                int index = i;
                tasks[i] = Task.Factory.StartNew(() => syncStart(index, config, cache), TaskCreationOptions.LongRunning);
            }

            // try to mitigate spam from MemoryCache
            for (int i = 0; i < 3; i++)
            { 
                GC.Collect(); 
            }

            var sw = Stopwatch.StartNew();
            mre.Set();
            Task.WaitAll(tasks);
            return sw.Elapsed;
        }
    }
}
