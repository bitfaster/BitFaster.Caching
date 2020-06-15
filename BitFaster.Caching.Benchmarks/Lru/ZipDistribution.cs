using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;

namespace BitFaster.Caching.Benchmarks.Lru
{
    public class ZipDistribution
    {
        const double s = 0.86;
        const int n = 500;
        const int sampleCount = 1000;
        private static int[] samples;

        const int concurrencyLevel = 1;
        const int cacheSize = 50; // 10% cache size

        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(concurrencyLevel, cacheSize, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(concurrencyLevel, cacheSize, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new ConcurrentTLru<int, int>(concurrencyLevel, cacheSize, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(concurrencyLevel, cacheSize, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(concurrencyLevel, cacheSize, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        [GlobalSetup]
        public void GlobalSetup()
        {
            samples = new int[sampleCount];
            Zipf.Samples(samples, s, n);
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = sampleCount)]
        public void ClassicLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < sampleCount; i++)
            {
                classicLru.GetOrAdd(samples[i], func);
            }
        }

        [Benchmark(OperationsPerInvoke = sampleCount)]
        public void FastConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < sampleCount; i++)
            {
                fastConcurrentLru.GetOrAdd(samples[i], func);
            }
        }

        [Benchmark(OperationsPerInvoke = sampleCount)]
        public void ConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < sampleCount; i++)
            {
                concurrentLru.GetOrAdd(samples[i], func);
            }
        }

        [Benchmark(OperationsPerInvoke = sampleCount)]
        public void FastConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < sampleCount; i++)
            {
                fastConcurrentTLru.GetOrAdd(samples[i], func);
            }
        }

        [Benchmark(OperationsPerInvoke = sampleCount)]
        public void ConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < sampleCount; i++)
            {
                concurrentTlru.GetOrAdd(samples[i], func);
            }
        }
    }
}
