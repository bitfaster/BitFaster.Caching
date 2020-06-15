using System;
using System.Collections.Generic;
using BitFaster.Caching.HitRateAnalysis;
using BitFaster.Caching.Lru;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;

namespace BitFaster.Sampling
{
    class Program
    {
        // Test methodolopy from 2Q paper:
        // http://www.vldb.org/conf/1994/P439.PDF

        // s = 0.5 and s = 0.86.
        // If there are N items, the probability of accessing an item numbered i or less is (i / N)^s. 
        // A setting of (s = 0.86 gives an 80 / 20 distribution, while a setting of (s = 0.5 give a less skewed 
        // distribution (about 45 / 20). 
        const double s = 0.86;
       // const double s = 0.5;

        // Took 1 million samples
        const int sampleCount = 1000000;

        // We simulated a database of 50,000 pages and
        // buffer sizes ranging from 2,500 (5%) items to 20,000
        // (40%) items.
        const int n = 50000;

        const double cacheSizeRatio = 0.05;

        const int cacheSize = (int)(n * cacheSizeRatio);

        static void Main(string[] args)
        {
            double[] sValues = { 0.5, 0.86 };
            double[] cacheSizes = { 0.025, 0.05, 0.1, 0.15, 0.2, 0.25, 0.3, 0.35, 0.4 };

            List<Analysis> analysis = new List<Analysis>();

            foreach (var sValue in sValues)
            {
                foreach (var cacheSize in cacheSizes)
                {
                    analysis.Add(new Analysis { 
                        N = n,
                        s = sValue,
                        Samples = sampleCount,
                        CacheSizePercent = cacheSize
                    });
                }
            }

            int[][] zipdfDistribution = new int[sValues.Length][];

            for (int i = 0; i < sValues.Length; i++)
            {
                Console.WriteLine($"Generating Zipfan distribution with {sampleCount} samples, s = {sValues[i]}, N = {n}");
                zipdfDistribution[i] = new int[sampleCount];
                Zipf.Samples(zipdfDistribution[i], sValues[i], n);
            }

            List<AnalysisResult> results = new List<AnalysisResult>();
            Func<int, int> func = x => x;

            foreach (var a in analysis)
            {
                a.WriteSummaryToConsole();

                int cacheSize = (int)(a.N * a.CacheSizePercent);

                var concurrentLru = new ConcurrentLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);
                var classicLru = new ClassicLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);

                var d = a.s == 0.5 ? 0 : 1;

                for (int i = 0; i < sampleCount; i++)
                {
                    concurrentLru.GetOrAdd(zipdfDistribution[d][i], func);
                    classicLru.GetOrAdd(zipdfDistribution[d][i], func);
                }

                results.Add(new AnalysisResult 
                {
                    Cache = "ClassicLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    HitRatio = classicLru.HitRatio * 100.0,
                });

                results.Add(new AnalysisResult
                {
                    Cache = "ConcurrentLru",
                    N = a.N,
                    s = a.s,
                    CacheSizePercent = a.CacheSizePercent * 100.0,
                    Samples = a.Samples,
                    HitRatio = concurrentLru.HitRatio * 100.0,
                });
            }

            AnalysisResult.WriteToConsole(results);
            AnalysisResult.WriteToFile("results.csv", results);

            //var samples = new int[sampleCount];
            //Zipf.Samples(samples, s, n);

            //var concurrentLru = new ConcurrentLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);
            //var classicLru = new ClassicLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);


            //Console.WriteLine($"Running {sampleCount} iterations");

            //for (int i = 0; i < sampleCount; i++)
            //{
            //    concurrentLru.GetOrAdd(samples[i], func);
            //    classicLru.GetOrAdd(samples[i], func);
            //}

            //Console.WriteLine($"ConcurrentLru hit ratio {concurrentLru.HitRatio * 100.0}%");
            //Console.WriteLine($"ClassicLru hit ratio {classicLru.HitRatio * 100.0}%");

            Console.ReadLine();
        }
    }
}
