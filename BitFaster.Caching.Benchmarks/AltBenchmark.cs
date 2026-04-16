using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks
{
    [MemoryDiagnoser]
    [HideColumns("Job", "Median", "RatioSD")]
    public class AltBenchmark
    {
        private static readonly ConcurrentLru<string, int> concurrentLru = new ConcurrentLru<string, int>(8, 9, EqualityComparer<string>.Default);

        [GlobalSetup]
        public void GlobalSetup()
        {
            concurrentLru.AddOrUpdate("1", 1);
        }

#if NET9_0_OR_GREATER

        [Benchmark(Baseline = true)]
        public int GetAlternateInline()
        {
            var alt = concurrentLru.GetAlternateLookup<ReadOnlySpan<char>>();
            alt.TryGet("1", out int value);
            return value;
        }

        [Benchmark()]
        public int GetAlternateNoInline()
        {
            var alt = concurrentLru.GetAlternateLookup2<ReadOnlySpan<char>>();
            alt.TryGet("1", out int value);
            return value;
        }
#endif
    }
}
