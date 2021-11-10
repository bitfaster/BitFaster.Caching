using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks.Lru
{
    /// <summary>
    /// Compare different implementations of the TLRU policy. In particular, which clock impl is fastest?
    /// </summary>
    public class TLruTimeBenchmark
    {
        private static readonly TemplateConcurrentLru<int, int, TimeStampedLruItem<int, int>, TLruDateTimePolicy<int, int>, NullHitCounter<int, int>> dateTimeTLru
            = new TemplateConcurrentLru<int, int, TimeStampedLruItem<int, int>, TLruDateTimePolicy<int, int>, NullHitCounter<int, int>>
                (1, 3, EqualityComparer<int>.Default, new TLruDateTimePolicy<int, int>(TimeSpan.FromSeconds(1)), new NullHitCounter<int, int>());

        private static readonly TemplateConcurrentLru<int, int, TickCountLruItem<int, int>, TLruTicksPolicy<int, int>, NullHitCounter<int, int>> tickCountTLru
            = new TemplateConcurrentLru<int, int, TickCountLruItem<int, int>, TLruTicksPolicy<int, int>, NullHitCounter<int, int>>
                (1, 3, EqualityComparer<int>.Default, new TLruTicksPolicy<int, int>(TimeSpan.FromSeconds(1)), new NullHitCounter<int, int>());

        private static readonly TemplateConcurrentLru<int, int, LongTickCountLruItem<int, int>, TLruLongTicksPolicy<int, int>, NullHitCounter<int, int>> stopwatchTLru
            = new TemplateConcurrentLru<int, int, LongTickCountLruItem<int, int>, TLruLongTicksPolicy<int, int>, NullHitCounter<int, int>>
                (1, 3, EqualityComparer<int>.Default, new TLruLongTicksPolicy<int, int>(TimeSpan.FromSeconds(1)), new NullHitCounter<int, int>());

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
