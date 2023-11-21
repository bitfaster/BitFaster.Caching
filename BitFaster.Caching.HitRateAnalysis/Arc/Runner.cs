using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.HitRateAnalysis.Zipfian;

namespace BitFaster.Caching.HitRateAnalysis.Arc
{
    public class Runner
    {
        private readonly RunnerConfig config;

        public Runner(RunnerConfig config)
        {
            this.config = config;
        }

        public async Task Run()
        {
            await this.config.File.DownloadIfNotExistsAsync();

            Console.WriteLine("Running...");
            
            var sw = Stopwatch.StartNew();

            int count = this.config.Analysis.First().CacheSize >= 1_000_000 ? AnalyzeLarge() : AnalyzeSmall();

            Console.WriteLine($"Tested {count} keys in {sw.Elapsed}");

            this.config.Analysis.WriteToConsole();
            Analysis<long>.WriteToFile(this.config.Name, this.config.Analysis);
            Analysis<long>.Plot(this.config.Name, this.config.Name, this.config.Analysis);
        }

        private int AnalyzeSmall()
        {
            int count = 0;
            foreach (var key in this.config.File.EnumerateFileData())
            {
                foreach (var a in this.config.Analysis)
                {
                    a.TestKey(key);
                }

                if (++count % 100000 == 0)
                {
                    Console.WriteLine($"Processed {count} keys...");
                }
            }

            return count;
        }

        // for very large cache sizes do multiple passes of the data,
        // else not everything can fit in memory.
        private int AnalyzeLarge()
        {
            int count = 0;

            foreach (var a in this.config.Analysis)
            {
                Console.WriteLine($"Analyzing cache size {a.CacheSize}");

                foreach (var key in this.config.File.EnumerateFileData())
                {
                    a.TestKey(key);

                    if (++count % 100000 == 0)
                    {
                        Console.WriteLine($"Processed {count} keys...");
                        GC.Collect();
                    }
                }

                GC.Collect(2, GCCollectionMode.Forced, true);
            }

            return count;
        }
    }
}
