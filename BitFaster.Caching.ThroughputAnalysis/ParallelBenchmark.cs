using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class ParallelBenchmark
    {
        public static TimeSpan Run(Action<int> action, int threads)
        {
            Task[] tasks = new Task[threads];
            ManualResetEventSlim mre = new ManualResetEventSlim();

            Action<int> syncStart = taskId =>
            {
                mre.Wait();
                action(taskId);
            };

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Factory.StartNew(() => syncStart(i), TaskCreationOptions.LongRunning);
            }

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
