using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks.Lru
{
    [DisassemblyDiagnoser(printSource: true)]
    [MemoryDiagnoser]
    public class LruJustTryGet
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));


        [GlobalSetup]
        public void GlobalSetup()
        {
            dictionary.TryAdd(1, 1);
            fastConcurrentLru.GetOrAdd(1, k => k);
            fastConcurrentTLru.GetOrAdd(1, k => k);
        }

        [Benchmark(Baseline = true)]
        public int ConcurrentDictionary()
        {
            dictionary.TryGetValue(1, out var value);
            return value;
        }

        [Benchmark()]
        public int FastConcurrentLru()
        {
            fastConcurrentLru.TryGet(1, out var value);
            return value;
        }

        [Benchmark()]
        public int FastConcurrentTLru()
        {
            fastConcurrentTLru.TryGet(1, out var value);
            return value;
        }
    }
}
