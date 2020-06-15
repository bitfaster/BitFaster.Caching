using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks.Lru
{
    public class TLruTimeBenchmark
    {
        private static readonly TemplateConcurrentLru<int, int, TimeStampedLruItem<int, int>, TLruDateTimePolicy<int, int>, NullHitCounter> dateTimeTLru
            = new TemplateConcurrentLru<int, int, TimeStampedLruItem<int, int>, TLruDateTimePolicy<int, int>, NullHitCounter>
                (1, 3, EqualityComparer<int>.Default, new TLruDateTimePolicy<int, int>(TimeSpan.FromSeconds(1)), new NullHitCounter());

        private static readonly TemplateConcurrentLru<int, int, TickCountLruItem<int, int>, TLruTicksPolicy<int, int>, NullHitCounter> tickCountTLru
            = new TemplateConcurrentLru<int, int, TickCountLruItem<int, int>, TLruTicksPolicy<int, int>, NullHitCounter>
                (1, 3, EqualityComparer<int>.Default, new TLruTicksPolicy<int, int>(TimeSpan.FromSeconds(1)), new NullHitCounter());

        private static readonly TemplateConcurrentLru<int, int, LongTickCountLruItem<int, int>, TLruLongTicksPolicy<int, int>, NullHitCounter> stopwatchTLru
            = new TemplateConcurrentLru<int, int, LongTickCountLruItem<int, int>, TLruLongTicksPolicy<int, int>, NullHitCounter>
                (1, 3, EqualityComparer<int>.Default, new TLruLongTicksPolicy<int, int>(TimeSpan.FromSeconds(1)), new NullHitCounter());

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
        public void StopWatchGetElapsed()
        {
            Func<int, int> func = x => x;
            stopwatchTLru.GetOrAdd(1, func);
        }
    }
}
