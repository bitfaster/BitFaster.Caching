using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Benchmarks
{
    [MemoryDiagnoser]
    [HideColumns("Job", "Median", "RatioSD")]
    public class AltBenchmark
    {
        private static readonly ConcurrentLru<string, int> concurrentLru = new ConcurrentLru<string, int>(8, 9, EqualityComparer<string>.Default);
        private static readonly ConcurrentLfu<string, int> concurrentLfu = new ConcurrentLfu<string, int>(8, 9, new ThreadPoolScheduler(), EqualityComparer<string>.Default);


        [GlobalSetup]
        public void GlobalSetup()
        {
            concurrentLru.AddOrUpdate("1", 1);
            concurrentLfu.AddOrUpdate("2", 2);
        }

#if NET9_0_OR_GREATER

        [Benchmark]
        public int LruGetAlternateInline()
        {
            var alt = concurrentLru.GetAlternateLookup<ReadOnlySpan<char>>();
            alt.TryGet("1", out int value);
            return value;
        }

        [Benchmark]
        public int LfuGetAlternateInline()
        {
            var alt = concurrentLfu.GetAlternateLookup<ReadOnlySpan<char>>();
            alt.TryGet("2", out int value);
            return value;
        }
#endif
    }
}
