using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using CsvHelper;

namespace BitFaster.Caching.HitRateAnalysis.Wikibench
{
    public class Analysis
    {
        private readonly ConcurrentLru<Uri, int> concurrentLru;
        private readonly ClassicLru<Uri, int> classicLru;

        public Analysis(int cacheSize)
        {
            this.concurrentLru = new ConcurrentLru<Uri, int>(1, cacheSize, EqualityComparer<Uri>.Default);
            this.classicLru = new ClassicLru<Uri, int>(1, cacheSize, EqualityComparer<Uri>.Default);
        }

        public int CacheSize => this.concurrentLru.Capacity;

        public double ConcurrentLruHitRate => this.concurrentLru.HitRatio * 100;

        public double ClassicLruHitRate => this.classicLru.HitRatio * 100;

        public void TestUri(Uri uri)
        {
            this.concurrentLru.GetOrAdd(uri, u => 1);
            this.classicLru.GetOrAdd(uri, u => 1);
        }

        public void Compare()
        {
            Console.WriteLine($"Size {this.concurrentLru.Capacity} Classic HitRate {FormatHits(this.classicLru.HitRatio)} Concurrent HitRate {FormatHits(this.concurrentLru.HitRatio)}");
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
