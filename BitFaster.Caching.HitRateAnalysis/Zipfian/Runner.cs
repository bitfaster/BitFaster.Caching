using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;

namespace BitFaster.Caching.HitRateAnalysis.Zipfian
{
    public class Runner
    {
        // Test methodolopy from 2Q paper:
        // http://www.vldb.org/conf/1994/P439.PDF

        // s = 0.5 and s = 0.86.
        // If there are N items, the probability of accessing an item numbered i or less is (i / N)^s. 
        // A setting of (s = 0.86 gives an 80 / 20 distribution, while a setting of (s = 0.5 give a less skewed 
        // distribution (about 45 / 20). 

        // Took 1 million samples
        const int sampleCount = 1000000;

        // Simulated a database of 50,000 pages and
        // buffer sizes ranging from 2,500 (5%) items to 20,000
        // (40%) items.
        const int n = 50000;

        public static void Run()
        {
            int[] sValuesIndex = { 0, 1 };
            double[] sValues = { 0.5, 0.86 };

            // % of total number of items
            double[] cacheSizes = { 0.0125, 0.025, 0.05, 0.075, 0.1, 0.125, 0.15, 0.2, 0.25, 0.3, 0.35, 0.4 };

            List<Analysis> analysis = new List<Analysis>();

            foreach (var sValue in sValues)
            {
                foreach (var cacheSize in cacheSizes)
                {
                    analysis.Add(new Analysis
                    {
                        N = n,
                        s = sValue,
                        Samples = sampleCount,
                        CacheSizePercent = cacheSize
                    });
                }
            }

            int[][] zipdfDistribution = new int[sValues.Length][];

            Parallel.ForEach(sValuesIndex, index =>
            {
                Console.WriteLine($"Generating Zipfian distribution with {sampleCount} samples, s = {sValues[index]}, N = {n}");
                var sw = Stopwatch.StartNew();
                zipdfDistribution[index] = new int[sampleCount];
                Zipf.Samples(zipdfDistribution[index], sValues[index], n);
                Console.WriteLine($"Took {sw.Elapsed} for s = {sValues[index]}.");
            });

            List<AnalysisResult> results = new List<AnalysisResult>();
            Func<int, int> func = x => x;

            foreach (var a in analysis)
            {
                a.WriteSummaryToConsole();

                int cacheSize = (int)(a.N * a.CacheSizePercent);

                var concurrentLru = new ConcurrentLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);
                var classicLru = new ClassicLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);

                var concurrentLruScan = new ConcurrentLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);
                var classicLruScan = new ClassicLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);

                var d = a.s == 0.5 ? 0 : 1;

                var lruSw = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    concurrentLru.GetOrAdd(zipdfDistribution[d][i], func);
                }
                lruSw.Stop();
                Console.WriteLine($"concurrentLru size={cacheSize} took {lruSw.Elapsed}.");

                var clruSw = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    classicLru.GetOrAdd(zipdfDistribution[d][i], func);
                }
                clruSw.Stop();
                Console.WriteLine($"classic lru size={cacheSize} took {clruSw.Elapsed}.");

                var lruSwScan = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    concurrentLruScan.GetOrAdd(zipdfDistribution[d][i], func);
                    concurrentLruScan.GetOrAdd(i % n, func);
                }
                lruSwScan.Stop();
                Console.WriteLine($"concurrentLruScan lru size={cacheSize} took {lruSwScan.Elapsed}.");

                var clruSwScan = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    classicLruScan.GetOrAdd(zipdfDistribution[d][i], func);
                    classicLruScan.GetOrAdd(i % n, func);
                }
                clruSwScan.Stop();
                Console.WriteLine($"classicLruScan lru size={cacheSize} took {clruSwScan.Elapsed}.");

                results.Add(new AnalysisResult
                {
                    Cache = "ClassicLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = false,
                    HitRatio = classicLru.HitRatio * 100.0,
                    Duration = clruSw.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "ConcurrentLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = false,
                    HitRatio = concurrentLru.Metrics.HitRatio * 100.0,
                    Duration = lruSw.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "ClassicLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = true,
                    HitRatio = classicLruScan.Metrics.HitRatio * 100.0,
                    Duration = clruSwScan.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "ConcurrentLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = true,
                    HitRatio = concurrentLruScan.Metrics.HitRatio * 100.0,
                    Duration = lruSwScan.Elapsed,
                });
            }

            results.WriteToConsole();
            AnalysisResult.WriteToFile("results.zipf.csv", results);

            Console.ReadLine();
        }
    }
}
