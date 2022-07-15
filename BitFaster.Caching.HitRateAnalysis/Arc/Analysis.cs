using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using CsvHelper;

namespace BitFaster.Caching.HitRateAnalysis.Arc
{
    public class Analysis
    {
        private readonly ConcurrentLru<long, int> concurrentLru;
        private readonly ClassicLru<long, int> classicLru;

        public Analysis(int cacheSize)
        {
            concurrentLru = new ConcurrentLru<long, int>(1, cacheSize, EqualityComparer<long>.Default);
            classicLru = new ClassicLru<long, int>(1, cacheSize, EqualityComparer<long>.Default);
        }

        public int CacheSize => concurrentLru.Capacity;

        public double ConcurrentLruHitRate => concurrentLru.HitRatio * 100;

        public double ClassicLruHitRate => classicLru.HitRatio * 100;

        public void TestKey(long key)
        {
            concurrentLru.GetOrAdd(key, u => 1);
            classicLru.GetOrAdd(key, u => 1);
        }

        public void Compare()
        {
            Console.WriteLine($"Size {concurrentLru.Capacity} Classic HitRate {FormatHits(classicLru.HitRatio)} Concurrent HitRate {FormatHits(concurrentLru.HitRatio)}");
        }

        private static string FormatHits(double hitRate)
        {
            return string.Format("{0:N2}%", hitRate * 100.0);
        }

        public static void WriteToFile(string path, IEnumerable<Analysis> results)
        {
            using (var writer = new StreamWriter(path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(results);
            }
        }
    }
}
