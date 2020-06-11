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

namespace Lightweight.Caching3.Benchmarks
{
    [MemoryDiagnoser]
    public class LruGetOrAddTest
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly Lightweight.Caching.SegmentedLru<int, int> segmentedLru = new Lightweight.Caching.SegmentedLru<int, int>(8, 3, 3, 3, EqualityComparer<int>.Default);
        private static readonly Lightweight.Caching2.SegmentedLruNoExpiration<int, int> segmentedLru2 = new Lightweight.Caching2.SegmentedLruNoExpiration<int, int>(8, 3, 3, 3, EqualityComparer<int>.Default);
        
        private static readonly ConcurrentLruTemplate<int, int, LruItem<int, int>, InfiniteTtl<int, int>, NoHitCounter> concurrentLru 
            = new ConcurrentLruTemplate<int, int, LruItem<int, int>, InfiniteTtl<int, int>, NoHitCounter>(
                8, 9, EqualityComparer<int>.Default, new InfiniteTtl<int, int>(), new NoHitCounter());

        private static readonly ConcurrentLruTemplate<int, int, LruItem<int, int>, InfiniteTtl<int, int>, HitCounter> concurrentLruHit
            = new ConcurrentLruTemplate<int, int, LruItem<int, int>, InfiniteTtl<int, int>, HitCounter>(
                8, 9, EqualityComparer<int>.Default, new InfiniteTtl<int, int>(), new HitCounter());

        private static readonly ConcurrentLru<int, int> concurrentLruNoExpire 
            = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLruWithExpiry<int, int, AbsoluteTtl<int, int>> concurrentLruExpire
            = new ConcurrentLruWithExpiry<int, int, AbsoluteTtl<int, int>>(8, 9, EqualityComparer<int>.Default, AbsoluteTtl<int, int>.FromMinutes(10));

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

        [Benchmark]
        public void SegmentedLruGetOrAdd()
        {
            Func<int, int> func = x => x;
            segmentedLru.GetOrAdd(1, func);
        }

        [Benchmark]
        public void ClassNoTtlPolicyGetOrAdd()
        {
            Func<int, int> func = x => x;
            segmentedLru2.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLruTemplGetOrAdd()
        {
            Func<int, int> func = x => x;
            concurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLruTemplHitGetOrAdd()
        {
            Func<int, int> func = x => x;
            concurrentLruHit.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLruGetOrAdd()
        {
            Func<int, int> func = x => x;
            concurrentLruNoExpire.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLruExpireGetOrAdd()
        {
            Func<int, int> func = x => x;
            concurrentLruExpire.GetOrAdd(1, func);
        }

        private int MyFunc(int i)
        {
            return i;
        }
    }
}
