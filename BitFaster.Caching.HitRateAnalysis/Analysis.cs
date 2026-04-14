using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;
using CsvHelper;
using Plotly.NET.CSharp;
using Plotly.NET.ImageExport;

namespace BitFaster.Caching.HitRateAnalysis
{
    public class Analysis<K>
    {
        private readonly ConcurrentLru<K, int> concurrentLru;
        private readonly ClassicLru<K, int> classicLru;
        private readonly ConcurrentLfu<K, int> concurrentLfu;
        private readonly MemoryCacheAdaptor<K, int> memoryCache;

        public Analysis(int cacheSize)
        {
            concurrentLru = new ConcurrentLru<K, int>(1, cacheSize, EqualityComparer<K>.Default);
            classicLru = new ClassicLru<K, int>(1, cacheSize, EqualityComparer<K>.Default);
            concurrentLfu = new ConcurrentLfu<K, int>(1, cacheSize, new ForegroundScheduler(), EqualityComparer<K>.Default);
            memoryCache = new MemoryCacheAdaptor<K, int>(cacheSize);
        }

        public int CacheSize => concurrentLru.Capacity;

        public double ClassicLruHitRate => classicLru.Metrics.Value.HitRatio * 100;

        public double MemoryCacheHitRate => memoryCache.Metrics.Value.HitRatio * 100;

        public double ConcurrentLruHitRate => concurrentLru.Metrics.Value.HitRatio * 100;

        public double ConcurrentLfuHitRate => concurrentLfu.Metrics.Value.HitRatio * 100;

        public void TestKey(K key)
        {
            concurrentLru.GetOrAdd(key, u => 1);
            classicLru.GetOrAdd(key, u => 1);
            concurrentLfu.GetOrAdd(key, u => 1);
            memoryCache.GetOrAdd(key, u => 1);
        }

        public static void WriteToFile(string path, IEnumerable<Analysis<K>> results)
        {
            using (var writer = new StreamWriter(path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(results);
            }
        }

        public static void Plot(string path, string title, IEnumerable<Analysis<K>> results)
        {
            var xAxis = results.Select(x => x.CacheSize).ToArray();

            var classic = Chart.Line<int, double, string>(xAxis, results.Select(x => x.ClassicLruHitRate), Name: "LRU", MarkerColor: Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.Limegreen));
            var lru = Chart.Line<int, double, string>(xAxis, results.Select(x => x.ConcurrentLruHitRate), Name: "ConcurrentLru", MarkerColor: Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.RoyalBlue));
            var lfu = Chart.Line<int, double, string>(xAxis, results.Select(x => x.ConcurrentLfuHitRate), Name: "ConcurrentLfu", MarkerColor: Plotly.NET.Color.fromRGB(255, 192, 0));
            var memory = Chart.Line<int, double, string>(xAxis, results.Select(x => x.MemoryCacheHitRate), Name: "MemoryCache", MarkerColor: Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.FireBrick));

            var combined = Chart.Combine(new[] { classic, lru, lfu, memory });

            combined
                .WithLayout(title)
                .WithoutVerticalGridlines()
                .WithAxisTitles("Cache Size", "Hit Rate (%)")
                .SaveSVG(Path.GetFileNameWithoutExtension(path), Width: 1000, Height: 600);
        }
    }
}
