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
            int keys = 0;
            var sw = Stopwatch.StartNew();

            foreach (var block in this.config.File.EnumerateFileData())
            {
                foreach (var a in this.config.Analysis)
                {
                    for (long i = block.Start; i < block.Start + block.Sequence; i++)
                    {
                        a.TestKey(i);
                    }

                    
                }
                keys += block.Sequence;

                if (++count % 100000 == 0)
                {
                    Console.WriteLine($"Processed {keys} keys...");
                }
            }

            Console.WriteLine($"Tested {keys} keys in {sw.Elapsed}");

            this.config.Analysis.WriteToConsole();
            Analysis.WriteToFile(this.config.Name, this.config.Analysis);
        }
    }
}
