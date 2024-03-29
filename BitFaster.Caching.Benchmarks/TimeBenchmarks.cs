﻿using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace BitFaster.Caching.Benchmarks
{
#if Windows
    [SimpleJob(RuntimeMoniker.Net48)]
#endif
    [SimpleJob(RuntimeMoniker.Net60)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class TimeBenchmarks
    {
        private static readonly Stopwatch sw = Stopwatch.StartNew();

        [Benchmark(Baseline = true)]
        public DateTime DateTimeUtcNow()
        {
            return DateTime.UtcNow;
        }

        [Benchmark()]
        public int EnvironmentTickCount()
        {
            return Environment.TickCount;
        }

        [Benchmark()]
        public long EnvironmentTickCount64()
        {
#if NETCOREAPP3_0_OR_GREATER
            return Environment.TickCount64;
#else
            return 0;
#endif
        }

        [Benchmark()]
        public long StopWatchGetElapsed()
        {
            return sw.ElapsedTicks;
        }

        [Benchmark()]
        public long StopWatchGetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }
    }
}
