using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Lightweight.Caching.Lru;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Text;

namespace Lightweight.Caching.Benchmarks.Lru
{
    [MemoryDiagnoser]
    public class LruCycle
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTLru = new ConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        private static MemoryCache memoryCache = System.Runtime.Caching.MemoryCache.Default;

        [GlobalSetup]
        public void GlobalSetup()
        {
            Func<int, int> func = x => x;

            dictionary.GetOrAdd(1, func);
            dictionary.GetOrAdd(2, func);
            dictionary.GetOrAdd(3, func);
            dictionary.GetOrAdd(4, func);
            dictionary.GetOrAdd(5, func);
            dictionary.GetOrAdd(6, func);

            memoryCache.Set("1", "test", new CacheItemPolicy());
            memoryCache.Set("2", "test", new CacheItemPolicy());
            memoryCache.Set("3", "test", new CacheItemPolicy());
            memoryCache.Set("4", "test", new CacheItemPolicy());
            memoryCache.Set("5", "test", new CacheItemPolicy());
            memoryCache.Set("6", "test", new CacheItemPolicy());

            classicLru.GetOrAdd(1, func);
            classicLru.GetOrAdd(2, func);
            classicLru.GetOrAdd(3, func);
            classicLru.GetOrAdd(4, func);
            classicLru.GetOrAdd(5, func);
            classicLru.GetOrAdd(6, func);

            concurrentLru.GetOrAdd(1, func);
            concurrentLru.GetOrAdd(2, func);
            concurrentLru.GetOrAdd(3, func);
            concurrentLru.GetOrAdd(4, func);
            concurrentLru.GetOrAdd(5, func);
            concurrentLru.GetOrAdd(6, func);

        }

        [Benchmark(Baseline = true, OperationsPerInvoke = 24)]
        public void ConcurrentDictionary()
        {
            Func<int, int> func = x => x;
            dictionary.GetOrAdd(1, func);
            dictionary.GetOrAdd(2, func);
            dictionary.GetOrAdd(3, func);
            dictionary.GetOrAdd(4, func);
            dictionary.GetOrAdd(5, func);
            dictionary.GetOrAdd(6, func);

            dictionary.GetOrAdd(1, func);
            dictionary.GetOrAdd(2, func);
            dictionary.GetOrAdd(3, func);
            dictionary.GetOrAdd(4, func);
            dictionary.GetOrAdd(5, func);
            dictionary.GetOrAdd(6, func);

            dictionary.GetOrAdd(1, func);
            dictionary.GetOrAdd(2, func);
            dictionary.GetOrAdd(3, func);
            dictionary.GetOrAdd(4, func);
            dictionary.GetOrAdd(5, func);
            dictionary.GetOrAdd(6, func);

            dictionary.GetOrAdd(1, func);
            dictionary.GetOrAdd(2, func);
            dictionary.GetOrAdd(3, func);
            dictionary.GetOrAdd(4, func);
            dictionary.GetOrAdd(5, func);
            dictionary.GetOrAdd(6, func);
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void FastConcurrentLru()
        {
            // size is 9, so segment size is 3. 6 items will cause queue cycling
            // without eviction. Hot => cold when not accessed.
            Func<int, int> func = x => x;
            fastConcurrentLru.GetOrAdd(1, func);
            fastConcurrentLru.GetOrAdd(2, func);
            fastConcurrentLru.GetOrAdd(3, func);
            fastConcurrentLru.GetOrAdd(4, func);
            fastConcurrentLru.GetOrAdd(5, func);
            fastConcurrentLru.GetOrAdd(6, func);

            fastConcurrentLru.GetOrAdd(1, func);
            fastConcurrentLru.GetOrAdd(2, func);
            fastConcurrentLru.GetOrAdd(3, func);
            fastConcurrentLru.GetOrAdd(4, func);
            fastConcurrentLru.GetOrAdd(5, func);
            fastConcurrentLru.GetOrAdd(6, func);

            fastConcurrentLru.GetOrAdd(1, func);
            fastConcurrentLru.GetOrAdd(2, func);
            fastConcurrentLru.GetOrAdd(3, func);
            fastConcurrentLru.GetOrAdd(4, func);
            fastConcurrentLru.GetOrAdd(5, func);
            fastConcurrentLru.GetOrAdd(6, func);

            fastConcurrentLru.GetOrAdd(1, func);
            fastConcurrentLru.GetOrAdd(2, func);
            fastConcurrentLru.GetOrAdd(3, func);
            fastConcurrentLru.GetOrAdd(4, func);
            fastConcurrentLru.GetOrAdd(5, func);
            fastConcurrentLru.GetOrAdd(6, func);
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ConcurrentLru()
        {
            // size is 9, so segment size is 3. 6 items will cause queue cycling
            // without eviction. Hot => cold when not accessed.
            Func<int, int> func = x => x;
            concurrentLru.GetOrAdd(1, func);
            concurrentLru.GetOrAdd(2, func);
            concurrentLru.GetOrAdd(3, func);
            concurrentLru.GetOrAdd(4, func);
            concurrentLru.GetOrAdd(5, func);
            concurrentLru.GetOrAdd(6, func);

            concurrentLru.GetOrAdd(1, func);
            concurrentLru.GetOrAdd(2, func);
            concurrentLru.GetOrAdd(3, func);
            concurrentLru.GetOrAdd(4, func);
            concurrentLru.GetOrAdd(5, func);
            concurrentLru.GetOrAdd(6, func);

            concurrentLru.GetOrAdd(1, func);
            concurrentLru.GetOrAdd(2, func);
            concurrentLru.GetOrAdd(3, func);
            concurrentLru.GetOrAdd(4, func);
            concurrentLru.GetOrAdd(5, func);
            concurrentLru.GetOrAdd(6, func);

            concurrentLru.GetOrAdd(1, func);
            concurrentLru.GetOrAdd(2, func);
            concurrentLru.GetOrAdd(3, func);
            concurrentLru.GetOrAdd(4, func);
            concurrentLru.GetOrAdd(5, func);
            concurrentLru.GetOrAdd(6, func);
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void FastConcurrentTLru()
        {
            // size is 9, so segment size is 3. 6 items will cause queue cycling
            // without eviction. Hot => cold when not accessed.
            Func<int, int> func = x => x;
            fastConcurrentTLru.GetOrAdd(1, func);
            fastConcurrentTLru.GetOrAdd(2, func);
            fastConcurrentTLru.GetOrAdd(3, func);
            fastConcurrentTLru.GetOrAdd(4, func);
            fastConcurrentTLru.GetOrAdd(5, func);
            fastConcurrentTLru.GetOrAdd(6, func);

            fastConcurrentTLru.GetOrAdd(1, func);
            fastConcurrentTLru.GetOrAdd(2, func);
            fastConcurrentTLru.GetOrAdd(3, func);
            fastConcurrentTLru.GetOrAdd(4, func);
            fastConcurrentTLru.GetOrAdd(5, func);
            fastConcurrentTLru.GetOrAdd(6, func);

            fastConcurrentTLru.GetOrAdd(1, func);
            fastConcurrentTLru.GetOrAdd(2, func);
            fastConcurrentTLru.GetOrAdd(3, func);
            fastConcurrentTLru.GetOrAdd(4, func);
            fastConcurrentTLru.GetOrAdd(5, func);
            fastConcurrentTLru.GetOrAdd(6, func);

            fastConcurrentTLru.GetOrAdd(1, func);
            fastConcurrentTLru.GetOrAdd(2, func);
            fastConcurrentTLru.GetOrAdd(3, func);
            fastConcurrentTLru.GetOrAdd(4, func);
            fastConcurrentTLru.GetOrAdd(5, func);
            fastConcurrentTLru.GetOrAdd(6, func);
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ConcurrentTLru()
        {
            // size is 9, so segment size is 3. 6 items will cause queue cycling
            // without eviction. Hot => cold when not accessed.
            Func<int, int> func = x => x;
            concurrentTLru.GetOrAdd(1, func);
            concurrentTLru.GetOrAdd(2, func);
            concurrentTLru.GetOrAdd(3, func);
            concurrentTLru.GetOrAdd(4, func);
            concurrentTLru.GetOrAdd(5, func);
            concurrentTLru.GetOrAdd(6, func);

            concurrentTLru.GetOrAdd(1, func);
            concurrentTLru.GetOrAdd(2, func);
            concurrentTLru.GetOrAdd(3, func);
            concurrentTLru.GetOrAdd(4, func);
            concurrentTLru.GetOrAdd(5, func);
            concurrentTLru.GetOrAdd(6, func);

            concurrentTLru.GetOrAdd(1, func);
            concurrentTLru.GetOrAdd(2, func);
            concurrentTLru.GetOrAdd(3, func);
            concurrentTLru.GetOrAdd(4, func);
            concurrentTLru.GetOrAdd(5, func);
            concurrentTLru.GetOrAdd(6, func);

            concurrentTLru.GetOrAdd(1, func);
            concurrentTLru.GetOrAdd(2, func);
            concurrentTLru.GetOrAdd(3, func);
            concurrentTLru.GetOrAdd(4, func);
            concurrentTLru.GetOrAdd(5, func);
            concurrentTLru.GetOrAdd(6, func);
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ClassicLru()
        {
            // size is 9, so segment size is 3. 6 items will cause queue cycling
            // without eviction. Hot => cold when not accessed.
            Func<int, int> func = x => x;
            classicLru.GetOrAdd(1, func);
            classicLru.GetOrAdd(2, func);
            classicLru.GetOrAdd(3, func);
            classicLru.GetOrAdd(4, func);
            classicLru.GetOrAdd(5, func);
            classicLru.GetOrAdd(6, func);

            classicLru.GetOrAdd(1, func);
            classicLru.GetOrAdd(2, func);
            classicLru.GetOrAdd(3, func);
            classicLru.GetOrAdd(4, func);
            classicLru.GetOrAdd(5, func);
            classicLru.GetOrAdd(6, func);

            classicLru.GetOrAdd(1, func);
            classicLru.GetOrAdd(2, func);
            classicLru.GetOrAdd(3, func);
            classicLru.GetOrAdd(4, func);
            classicLru.GetOrAdd(5, func);
            classicLru.GetOrAdd(6, func);

            classicLru.GetOrAdd(1, func);
            classicLru.GetOrAdd(2, func);
            classicLru.GetOrAdd(3, func);
            classicLru.GetOrAdd(4, func);
            classicLru.GetOrAdd(5, func);
            classicLru.GetOrAdd(6, func);
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void MemoryCache()
        {
            memoryCache.Get("1");
            memoryCache.Get("2");
            memoryCache.Get("3");
            memoryCache.Get("4");
            memoryCache.Get("5");
            memoryCache.Get("6");

            memoryCache.Get("1");
            memoryCache.Get("2");
            memoryCache.Get("3");
            memoryCache.Get("4");
            memoryCache.Get("5");
            memoryCache.Get("6");

            memoryCache.Get("1");
            memoryCache.Get("2");
            memoryCache.Get("3");
            memoryCache.Get("4");
            memoryCache.Get("5");
            memoryCache.Get("6");

            memoryCache.Get("1");
            memoryCache.Get("2");
            memoryCache.Get("3");
            memoryCache.Get("4");
            memoryCache.Get("5");
            memoryCache.Get("6");
        }
    }
}
