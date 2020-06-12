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
         
        private static readonly ConcurrentLruTemplate<int, int, LruItem<int, int>, LruPolicy<int, int>, NullHitCounter> templateConcurrentLru 
            = new ConcurrentLruTemplate<int, int, LruItem<int, int>, LruPolicy<int, int>, NullHitCounter>(
                8, 9, EqualityComparer<int>.Default, new LruPolicy<int, int>(), new NullHitCounter());

        private static readonly ConcurrentLru<int, int> concurrentLru 
            = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentTLru<int, int> concurrentTlru
            = new ConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));

        private static readonly int key = 1;
        private static MemoryCache memoryCache = MemoryCache.Default;

        [GlobalSetup]
        public void GlobalSetup()
        {
            memoryCache.Set(key.ToString(), "test", new CacheItemPolicy());
        }

        [Benchmark(Baseline = true)]
        public void DictionaryGetOrAdd()
        {
            Func<int, int> func = x => x;
            dictionary.GetOrAdd(1, func);
        }

        [Benchmark()]
        public DateTime DateTimeUtcNow()
        {
            return DateTime.UtcNow;
        }

        [Benchmark()]
        public void MemoryCacheGetIntKey()
        {
            memoryCache.Get(key.ToString());
        }

        [Benchmark()]
        public void MemoryCacheGetStringKey()
        {
            memoryCache.Get("1");
        }

        [Benchmark()]
        public void ConcurrentLruNoCountGetOrAdd()
        {
            Func<int, int> func = x => x;
            templateConcurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLruGetOrAdd()
        {
            Func<int, int> func = x => x;
            concurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentTLruGetOrAdd()
        {
            Func<int, int> func = x => x;
            concurrentTlru.GetOrAdd(1, func);
        }
    }
}
