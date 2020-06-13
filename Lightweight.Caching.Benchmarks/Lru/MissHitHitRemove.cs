using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Text;
using BenchmarkDotNet.Attributes;
using Lightweight.Caching.Lru;

namespace Lightweight.Caching.Benchmarks.Lru
{
    [MemoryDiagnoser]
    public class MissHitHitRemove
    {
        private static readonly ConcurrentDictionary<int, byte[]> dictionary = new ConcurrentDictionary<int, byte[]>(8, 9, EqualityComparer<int>.Default);

        private static readonly ClassicLru<int, byte[]> classicLru = new ClassicLru<int, byte[]>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, byte[]> concurrentLru = new ConcurrentLru<int, byte[]>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, byte[]> concurrentTlru = new ConcurrentTLru<int, byte[]>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, byte[]> fastConcurrentLru = new FastConcurrentLru<int, byte[]>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, byte[]> fastConcurrentTLru = new FastConcurrentTLru<int, byte[]>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        private static MemoryCache memoryCache = System.Runtime.Caching.MemoryCache.Default;

        [Benchmark(Baseline = true)]
        public void ConcurrentDictionary()
        {
            Func<int, byte[]> func = x => new byte[16];
            dictionary.GetOrAdd(1, func);

            dictionary.GetOrAdd(1, func);
            dictionary.GetOrAdd(1, func);

            dictionary.TryRemove(1, out var removed);
        }

        [Benchmark()]
        public void FastConcurrentLru()
        {
            Func<int, byte[]> func = x => new byte[16];
            fastConcurrentLru.GetOrAdd(1, func);

            fastConcurrentLru.GetOrAdd(1, func);
            fastConcurrentLru.GetOrAdd(1, func);

            fastConcurrentLru.TryRemove(1);
        }

        [Benchmark()]
        public void ConcurrentLru()
        {
            Func<int, byte[]> func = x => new byte[16];
            concurrentLru.GetOrAdd(1, func);

            concurrentLru.GetOrAdd(1, func);
            concurrentLru.GetOrAdd(1, func);

            concurrentLru.TryRemove(1);
        }

        [Benchmark()]
        public void FastConcurrentTlru()
        {
            Func<int, byte[]> func = x => new byte[16];
            fastConcurrentTLru.GetOrAdd(1, func);

            fastConcurrentTLru.GetOrAdd(1, func);
            fastConcurrentTLru.GetOrAdd(1, func);

            fastConcurrentTLru.TryRemove(1);
        }

        [Benchmark()]
        public void ConcurrentTlru()
        {
            Func<int, byte[]> func = x => new byte[16];
            concurrentTlru.GetOrAdd(1, func);

            concurrentTlru.GetOrAdd(1, func);
            concurrentTlru.GetOrAdd(1, func);

            concurrentTlru.TryRemove(1);
        }

        [Benchmark()]
        public void ClassicLru()
        {
            Func<int, byte[]> func = x => new byte[16];
            classicLru.GetOrAdd(1, func);

            classicLru.GetOrAdd(1, func);
            classicLru.GetOrAdd(1, func);

            classicLru.TryRemove(1);
        }

        [Benchmark()]
        public void MemoryCache()
        {
            if (memoryCache.Get("1") == null)
            {
                memoryCache.Set("1", new byte[16], new CacheItemPolicy());
            }

            if (memoryCache.Get("1") == null)
            {
                memoryCache.Set("1", new byte[16], new CacheItemPolicy());
            }

            if (memoryCache.Get("1") == null)
            {
                memoryCache.Set("1", new byte[16], new CacheItemPolicy());
            }

            memoryCache.Remove("1");
        }
    }
}
