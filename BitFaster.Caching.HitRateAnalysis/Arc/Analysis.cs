using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using ConsoleTables;
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

        public static void WriteToFile(string path, IEnumerable<Analysis> results)
        {
            using (var writer = new StreamWriter(path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(results);
            }
        }

        public static void WriteToConsole(IEnumerable<Analysis> results)
        {
            ConsoleTable
                .From(results)
                .Configure(o => o.NumberAlignment = Alignment.Right)
                .Write(Format.MarkDown);
        }
    }
}
