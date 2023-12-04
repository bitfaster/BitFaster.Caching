﻿using System;
using System.Diagnostics;
using System.Threading;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class ParallelBenchmark
    {
        public static TimeSpan Run(Action<int> action, int threadCount)
        {
            Thread[] threads = new Thread[threadCount];
            using ManualResetEventSlim signalStart = new ManualResetEventSlim();

            Action<int> syncStart = (taskId) =>
            {
                signalStart.Wait();
                action(taskId);
            };

            for (int i = 0; i < threads.Length; i++)
            {
                int index = i;
                threads[i] = new Thread(() => syncStart(index));
                threads[i].Start();
            }

            // try to mitigate spam from MemoryCache
            for (int i = 0; i < 3; i++)
            { 
                GC.Collect(); 
            }

            var sw = Stopwatch.StartNew();
            signalStart.Set();
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            return sw.Elapsed;
        }
    }
}
