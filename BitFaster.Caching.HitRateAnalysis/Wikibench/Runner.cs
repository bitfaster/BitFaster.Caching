using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace BitFaster.Caching.HitRateAnalysis.Wikibench
{
    // See http://www.wikibench.eu/ for data
    // Should result in the same URLs as the parser here:
    // https://github.com/ben-manes/caffeine/blob/master/simulator/src/main/java/com/github/benmanes/caffeine/cache/simulator/parser/wikipedia/WikipediaTraceReader.java
    public class Runner
    {
        public static async Task Run()
        {
            int[] cacheSizes = { 25, 50, 75, 100, 125, 150, 175, 200 };
            var analysis = cacheSizes.Select(s => new Analysis(s)).ToList();

            string[] wikiUris =
                {
                "http://www.wikibench.eu/wiki/2007-09/wiki.1190153705.gz",
                "http://www.wikibench.eu/wiki/2007-09/wiki.1190157306.gz",
                "http://www.wikibench.eu/wiki/2007-09/wiki.1190160907.gz",
                "http://www.wikibench.eu/wiki/2007-09/wiki.1190164508.gz",
                "http://www.wikibench.eu/wiki/2007-09/wiki.1190168109.gz",
            };
            var dataSet = new WikiDataSet(wikiUris);
            await dataSet.DownloadIfNotExistsAsync();

            Console.WriteLine("Running...");
            int count = 0;
            var sw = Stopwatch.StartNew();

            foreach (var url in dataSet.EnumerateUris())
            {
                foreach (var a in analysis)
                {
                    a.TestUri(url);
                }

                if (count++ % 100000 == 0)
                {
                    Console.WriteLine($"Processed {count} URIs...");
                }
            }

            Console.WriteLine($"Tested {count} URIs in {sw.Elapsed}");

            foreach (var a in analysis)
            {
                a.Compare();
            }

            Analysis.WriteToFile("results.wikibench.csv", analysis);
        }
    }
}



