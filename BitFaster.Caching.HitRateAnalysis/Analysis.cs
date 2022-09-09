using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;
using CsvHelper;

namespace BitFaster.Caching.HitRateAnalysis
{
    public class Analysis<K>
    {
        private readonly ConcurrentLru<K, int> concurrentLru;
        private readonly ClassicLru<K, int> classicLru;
        private readonly ConcurrentLfu<K, int> concurrentLfu;

        public Analysis(int cacheSize)
        {
            concurrentLru = new ConcurrentLru<K, int>(1, cacheSize, EqualityComparer<K>.Default);
            classicLru = new ClassicLru<K, int>(1, cacheSize, EqualityComparer<K>.Default);
            concurrentLfu = new ConcurrentLfu<K, int>(1, cacheSize, new ForegroundScheduler(), EqualityComparer<K>.Default);
        }

        public int CacheSize => concurrentLru.Capacity;

        public double ClassicLruHitRate => classicLru.Metrics.Value.HitRatio * 100;

        public double ConcurrentLruHitRate => concurrentLru.Metrics.Value.HitRatio * 100;

        public double ConcurrentLfuHitRate => concurrentLfu.Metrics.Value.HitRatio * 100;

        public void TestKey(K key)
        {
            concurrentLru.GetOrAdd(key, u => 1);
            classicLru.GetOrAdd(key, u => 1);
            concurrentLfu.GetOrAdd(key, u => 1);
        }

        public static void WriteToFile(string path, IEnumerable<Analysis<K>> results)
        {
            using (var writer = new StreamWriter(path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(results);
            }
        }
    }
}
