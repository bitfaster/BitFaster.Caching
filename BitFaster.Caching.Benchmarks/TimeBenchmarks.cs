using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace BitFaster.Caching.Benchmarks
{
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
        public long StopWatchGetElapsed()
        {
            return sw.ElapsedTicks;
        }
    }
}
