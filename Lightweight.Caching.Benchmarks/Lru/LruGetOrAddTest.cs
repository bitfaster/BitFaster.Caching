using BenchmarkDotNet.Attributes;
using Lightweight.Caching;
using Lightweight.Caching.Lru;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Benchmarks
{
    [MemoryDiagnoser]
    public class LruGetOrAddTest
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new ConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        private static readonly int key = 1;
        private static MemoryCache memoryCache = MemoryCache.Default;

        [GlobalSetup]
        public void GlobalSetup()
        {
            memoryCache.Set(key.ToString(), "test", new CacheItemPolicy());
        }

        [Benchmark(Baseline = true)]
        public void ConcurrentDictionaryGetOrAdd()
        {
            Func<int, int> func = x => x;
            dictionary.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void FastConcurrentLruGetOrAdd()
        {
            Func<int, int> func = x => x;
            fastConcurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLruGetOrAdd()
        {
            Func<int, int> func = x => x;
            concurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void FastConcurrentTLruGetOrAdd()
        {
            Func<int, int> func = x => x;
            fastConcurrentTLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentTLruGetOrAdd()
        {
            Func<int, int> func = x => x;
            concurrentTlru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ClassicLruGetOrAdd()
        {
            Func<int, int> func = x => x;
            classicLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void MemoryCacheGetStringKey()
        {
            memoryCache.Get("1");
        }
    }
}
