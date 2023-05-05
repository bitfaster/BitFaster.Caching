using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.ThroughputAnalysis;
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
        const int sampleCount = 1_000_000;

        // Simulated a database of 50,000 pages and
        // buffer sizes ranging from 2,500 (5%) items to 20,000
        // (40%) items.
        const int n = 50_000;

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

            long[][] zipdfDistribution = new long[sValues.Length][];

            Parallel.ForEach(sValuesIndex, index =>
            {
                Console.WriteLine($"Generating Zipfian distribution with {sampleCount} samples, s = {sValues[index]}, N = {n}");
                var sw = Stopwatch.StartNew();
                zipdfDistribution[index] = FastZipf.Generate(new Random(666), sampleCount, sValues[index], n);
                Console.WriteLine($"Took {sw.Elapsed} for s = {sValues[index]}.");
            });

            List<AnalysisResult> results = new List<AnalysisResult>();
            Func<long, int> func = x => (int)x;

            foreach (var a in analysis)
            {
                a.WriteSummaryToConsole();

                int cacheSize = (int)(a.N * a.CacheSizePercent);

                var concurrentLru = new ConcurrentLru<long, int>(1, cacheSize, EqualityComparer<long>.Default);
                var classicLru = new ClassicLru<long, int>(1, cacheSize, EqualityComparer<long>.Default);
                var memCache = new MemoryCacheAdaptor<long, int>(cacheSize);
                var concurrentLfu = new ConcurrentLfu<long, int>(cacheSize);

                var concurrentLruScan = new ConcurrentLru<long, int>(1, cacheSize, EqualityComparer<long>.Default);
                var classicLruScan = new ClassicLru<long, int>(1, cacheSize, EqualityComparer<long>.Default);
                var memCacheScan = new MemoryCacheAdaptor<long, int>(cacheSize);
                var concurrentLfuScan = new ConcurrentLfu<long, int>(cacheSize);

                var d = a.s == 0.5 ? 0 : 1;

                var lruSw = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    concurrentLru.GetOrAdd(zipdfDistribution[d][i], func);
                }
                lruSw.Stop();
                Console.WriteLine($"concurrentLru size={cacheSize} took {lruSw.Elapsed}.");

                var lfuSw = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    concurrentLfu.GetOrAdd(zipdfDistribution[d][i], func);
                }
                lfuSw.Stop();
                Console.WriteLine($"concurrentLfu size={cacheSize} took {lfuSw.Elapsed}.");

                var clruSw = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    classicLru.GetOrAdd(zipdfDistribution[d][i], func);
                }
                clruSw.Stop();
                Console.WriteLine($"classic lru size={cacheSize} took {clruSw.Elapsed}.");

                var memSw = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    memCache.GetOrAdd(zipdfDistribution[d][i], func);
                }
                memSw.Stop();
                Console.WriteLine($"memcache size={cacheSize} took {memSw.Elapsed}.");

                var lruSwScan = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    concurrentLruScan.GetOrAdd(zipdfDistribution[d][i], func);
                    concurrentLruScan.GetOrAdd(i % n, func);
                }
                lruSwScan.Stop();
                Console.WriteLine($"concurrentLruScan lru size={cacheSize} took {lruSwScan.Elapsed}.");

                var lfuSwScan = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    concurrentLfuScan.GetOrAdd(zipdfDistribution[d][i], func);
                    concurrentLfuScan.GetOrAdd(i % n, func);
                }
                lfuSwScan.Stop();
                Console.WriteLine($"concurrentLfuScan lru size={cacheSize} took {lfuSwScan.Elapsed}.");

                var clruSwScan = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    classicLruScan.GetOrAdd(zipdfDistribution[d][i], func);
                    classicLruScan.GetOrAdd(i % n, func);
                }
                clruSwScan.Stop();
                Console.WriteLine($"classicLruScan lru size={cacheSize} took {clruSwScan.Elapsed}.");

                var memSwScan = Stopwatch.StartNew();
                for (int i = 0; i < sampleCount; i++)
                {
                    memCacheScan.GetOrAdd(zipdfDistribution[d][i], func);
                    memCacheScan.GetOrAdd(i % n, func);
                }
                memSwScan.Stop();
                Console.WriteLine($"memcacheScan size={cacheSize} took {memSwScan.Elapsed}.");

                results.Add(new AnalysisResult
                {
                    Cache = "ClassicLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = false,
                    HitRatio = classicLru.Metrics.Value.HitRatio * 100.0,
                    Duration = clruSw.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "MemoryCache",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = false,
                    HitRatio = memCache.Metrics.Value.HitRatio * 100.0,
                    Duration = memSw.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "ConcurrentLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = false,
                    HitRatio = concurrentLru.Metrics.Value.HitRatio * 100.0,
                    Duration = lruSw.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "ConcurrentLfu",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = false,
                    HitRatio = concurrentLfu.Metrics.Value.HitRatio * 100.0,
                    Duration = lfuSw.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "ClassicLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = true,
                    HitRatio = classicLruScan.Metrics.Value.HitRatio * 100.0,
                    Duration = clruSwScan.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "MemoryCache",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = true,
                    HitRatio = memCacheScan.Metrics.Value.HitRatio * 100.0,
                    Duration = memSwScan.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "ConcurrentLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = true,
                    HitRatio = concurrentLruScan.Metrics.Value.HitRatio * 100.0,
                    Duration = lruSwScan.Elapsed,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "ConcurrentLfu",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    IsScan = true,
                    HitRatio = concurrentLfuScan.Metrics.Value.HitRatio * 100.0,
                    Duration = lfuSwScan.Elapsed,
                });
            }

            results.WriteToConsole();
            AnalysisResult.WriteToFile("results.zipf.csv", results);

            Console.ReadLine();
        }
    }
}
