using BenchmarkDotNet.Attributes;
using BitFaster.Caching;
using BitFaster.Caching.Benchmarks.Lru;
using BitFaster.Caching.Lru;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Benchmarks
{
    [MemoryDiagnoser]
    public class LruJustGet
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new ConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        private static readonly int key = 1;
        private static System.Runtime.Caching.MemoryCache memoryCache = System.Runtime.Caching.MemoryCache.Default;

        Microsoft.Extensions.Caching.Memory.MemoryCache exMemoryCache 
            = new Microsoft.Extensions.Caching.Memory.MemoryCache(new MemoryCacheOptionsAccessor());

        [GlobalSetup]
        public void GlobalSetup()
        {
            memoryCache.Set(key.ToString(), "test", new System.Runtime.Caching.CacheItemPolicy());
            exMemoryCache.Set(key, "test");
        }

        [Benchmark(Baseline = true)]
        public void ConcurrentDictionary()
        {
            Func<int, int> func = x => x;
            dictionary.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void FastConcurrentLru()
        {
            Func<int, int> func = x => x;
            fastConcurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLru()
        {
            Func<int, int> func = x => x;
            concurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void FastConcurrentTLru()
        {
            Func<int, int> func = x => x;
            fastConcurrentTLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentTLru()
        {
            Func<int, int> func = x => x;
            concurrentTlru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ClassicLru()
        {
            Func<int, int> func = x => x;
            classicLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void RuntimeMemoryCacheGet()
        {
            memoryCache.Get("1");
        }

        [Benchmark()]
        public void ExtensionsMemoryCacheGet()
        {
            exMemoryCache.Get(1);
        }

        public class MemoryCacheOptionsAccessor
            : Microsoft.Extensions.Options.IOptions<MemoryCacheOptions>
        {
            private readonly MemoryCacheOptions options = new MemoryCacheOptions();

            public MemoryCacheOptions Value => this.options;

        }
    }
}
