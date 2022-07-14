using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.HitRateAnalysis.Glimpse
{
    public class Runner
    {
        public static async Task Run()
        {
            int[] cacheSizes = { 250, 500, 750, 1000, 1250, 1500, 1750, 2000 };
            var analysis = cacheSizes.Select(s => new Analysis(s)).ToList();

            await DataFile.DownloadIfNotExistsAsync();

            Console.WriteLine("Running...");
            int count = 0;
            var sw = Stopwatch.StartNew();

            foreach (var key in DataFile.EnumerateFileData())
            {
                foreach (var a in analysis)
                {
                    a.TestKey(key);
                }

                if (count++ % 100000 == 0)
                {
                    Console.WriteLine($"Processed {count} keys...");
                }
            }

            Console.WriteLine($"Tested {count} keys in {sw.Elapsed}");

            foreach (var a in analysis)
            {
                a.Compare();
            }

            Analysis.WriteToFile("results.glimpse.csv", analysis);
        }
    }
}
