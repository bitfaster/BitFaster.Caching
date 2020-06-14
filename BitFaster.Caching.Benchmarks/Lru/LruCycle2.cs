using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Text;

namespace BitFaster.Caching.Benchmarks.Lru
{
    [MemoryDiagnoser]
    public class LruCycle2
    {
        const int capacity = 9;
        const int iters = 10;

        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(8, capacity, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, capacity, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTLru = new ConcurrentTLru<int, int>(8, capacity, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, capacity, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, capacity, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        private static MemoryCache memoryCache = System.Runtime.Caching.MemoryCache.Default;

        [Benchmark(Baseline = true, OperationsPerInvoke = 24)]
        public void ConcurrentDictionary()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < iters; j++)
            for (int i = 0; i < capacity + 1; i++)
            { 
                dictionary.GetOrAdd(i, func); 

                // simulate what the LRU does
                if (i == capacity)
                {
                    dictionary.TryRemove(1, out var removed);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void FastConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < iters; j++)
                for (int i = 0; i < capacity + 1; i++)
            {
                fastConcurrentLru.GetOrAdd(i, func);
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < iters; j++)
                for (int i = 0; i < capacity + 1; i++)
            {
                concurrentLru.GetOrAdd(i, func);
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void FastConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < iters; j++)
                for (int i = 0; i < capacity + 1; i++)
            {
                fastConcurrentTLru.GetOrAdd(i, func);
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < iters; j++)
                for (int i = 0; i < capacity + 1; i++)
            {
                concurrentTLru.GetOrAdd(i, func);
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ClassicLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < iters; j++)
                for (int i = 0; i < capacity + 1; i++)
            {
                classicLru.GetOrAdd(i, func);
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void MemoryCache()
        {

            Func<int, int> func = x => x;

            for (int j = 0; j < iters; j++)
                for (int i = 0; i < capacity + 1; i++)
            {
                string key = i.ToString();
                var v = memoryCache.Get(key);

                if (v == null)
                { 
                    memoryCache.Set(key, "test", new CacheItemPolicy()); 
                }

                // simulate what the LRU does
                if (i == capacity)
                {
                    memoryCache.Remove("1");
                }
            }
        }
    }
}
