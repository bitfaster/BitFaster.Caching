using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.HitRateAnalysis.Arc
{
    public class Runner
    {
        private readonly RunnerConfig config;

        public Runner(RunnerConfig config)
        {
            this.config = config;
        }

        public  async Task Run()
        {
            await this.config.File.DownloadIfNotExistsAsync();

            Console.WriteLine("Running...");
            int count = 0;
            var sw = Stopwatch.StartNew();

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

            Console.WriteLine($"Tested {count} keys in {sw.Elapsed}");

            Analysis.WriteToConsole(this.config.Analysis);
            Analysis.WriteToFile(this.config.Name, this.config.Analysis);
        }
    }
}
