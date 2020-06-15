using System;
using System.Collections.Generic;
using BitFaster.Caching.Lru;
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
        const int sampleCount = 20000;

        // We simulated a database of 50,000 pages and
        // buffer sizes ranging from 2,500 (5%) items to 20,000
        // (40%) items.
        const int n = 50000;

        const double cacheSizeRatio = 0.05;

        const int cacheSize = (int)(n * cacheSizeRatio);

        static void Main(string[] args)
        {
            Console.WriteLine($"Generating Zipfan distribution with {sampleCount} samples, s = {s}, N = {n}");

            var samples = new int[sampleCount];
            Zipf.Samples(samples, s, n);

            var concurrentLru = new ConcurrentLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);
            var classicLru = new ClassicLru<int, int>(1, cacheSize, EqualityComparer<int>.Default);

            Func<int, int> func = x => x;
            Console.WriteLine($"Running {sampleCount} iterations");

            for (int i = 0; i < sampleCount; i++)
            {
                concurrentLru.GetOrAdd(samples[i], func);
                classicLru.GetOrAdd(samples[i], func);
            }

            Console.WriteLine($"ConcurrentLru hit ratio {concurrentLru.HitRatio * 100.0}%");
            Console.WriteLine($"ClassicLru hit ratio {classicLru.HitRatio * 100.0}%");

            Console.ReadLine();
        }
    }
}
