﻿using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks.Lru
{
    /// <summary>
    /// Compare different implementations of the TLRU policy. In particular, which clock impl is fastest?
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    public class TLruTimeBenchmark
    {
        private static readonly TemplateConcurrentLru<int, int, TimeStampedLruItem<int, int>, TLruDateTimePolicy<int, int>, NoTelemetryPolicy<int, int>> dateTimeTLru
            = new TemplateConcurrentLru<int, int, TimeStampedLruItem<int, int>, TLruDateTimePolicy<int, int>, NoTelemetryPolicy<int, int>>
                (1, new EqualCapacityPartition(3), EqualityComparer<int>.Default, new TLruDateTimePolicy<int, int>(TimeSpan.FromSeconds(1)), default);

        private static readonly TemplateConcurrentLru<int, int, TickCountLruItem<int, int>, TLruTicksPolicy<int, int>, NoTelemetryPolicy<int, int>> tickCountTLru
            = new TemplateConcurrentLru<int, int, TickCountLruItem<int, int>, TLruTicksPolicy<int, int>, NoTelemetryPolicy<int, int>>
                (1, new EqualCapacityPartition(3), EqualityComparer<int>.Default, new TLruTicksPolicy<int, int>(TimeSpan.FromSeconds(1)), default);

        private static readonly TemplateConcurrentLru<int, int, LongTickCountLruItem<int, int>, TLruLongTicksPolicy<int, int>, NoTelemetryPolicy<int, int>> stopwatchTLru
            = new TemplateConcurrentLru<int, int, LongTickCountLruItem<int, int>, TLruLongTicksPolicy<int, int>, NoTelemetryPolicy<int, int>>
                (1, new EqualCapacityPartition(3), EqualityComparer<int>.Default, new TLruLongTicksPolicy<int, int>(TimeSpan.FromSeconds(1)), default);

        [Benchmark(Baseline = true)]
        public void DateTimeUtcNow()
        {
            Func<int, int> func = x => x;
            dateTimeTLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void EnvironmentTickCount()
        {
            Func<int, int> func = x => x;
            tickCountTLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void StopWatchGetTimestamp()
        {
            Func<int, int> func = x => x;
            stopwatchTLru.GetOrAdd(1, func);
        }
    }
}
