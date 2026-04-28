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
    // The get alternate lookup family of methods return an interface, while under the hood they return a struct.
    // This benchmark verifies whether the JIT can devirtualize the return type to avoid boxing the struct.
    // It is not currently possible to avoid allocs for TryGet.
    [MemoryDiagnoser]
    [HideColumns("Job", "Median", "RatioSD", "Error", "StdDev")]
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
        public int LruGetAlternate()
        {
            var alt = concurrentLru.GetAlternateLookup<ReadOnlySpan<char>>();
            alt.TryGet("1", out int value);
            return value;
        }

        [Benchmark]
        public int LruTryGetAlternate()
        {
            concurrentLru.TryGetAlternateLookup<ReadOnlySpan<char>>(out var alt);
            alt.TryGet("1", out int value);
            return value;
        }

        [Benchmark]
        public int LfuGetAlternate()
        {
            var alt = concurrentLfu.GetAlternateLookup<ReadOnlySpan<char>>();
            alt.TryGet("2", out int value);
            return value;
        }

        [Benchmark]
        public int LfuTryGetAlternate()
        {
            concurrentLfu.TryGetAlternateLookup<ReadOnlySpan<char>>(out var alt);
            alt.TryGet("1", out int value);
            return value;
        }
#endif
    }
}
