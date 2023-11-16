using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lru;
using MathNet.Numerics.Distributions;

namespace BitFaster.Caching.Benchmarks.Lru
{
    //BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
    //Intel Xeon W-2133 CPU 3.60GHz, 1 CPU, 12 logical and 6 physical cores
    //.NET SDK= 6.0.100
    //  [Host]     : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
    //  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT


    //|             Method |     Mean |   Error |  StdDev | Ratio | RatioSD |  Gen 0 | Code Size | Allocated |
    //|------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|----------:|
    //|         ClassicLru | 111.3 ns | 1.33 ns | 1.11 ns |  1.00 |    0.00 | 0.0148 |   4,108 B |      64 B |
    //|  FastConcurrentLru | 121.6 ns | 1.45 ns | 1.21 ns |  1.09 |    0.01 | 0.0090 |   5,085 B |      39 B |
    //|      ConcurrentLru | 127.4 ns | 0.51 ns | 0.48 ns |  1.14 |    0.01 | 0.0093 |   5,107 B |      41 B |
    //| FastConcurrentTLru | 175.6 ns | 1.08 ns | 1.01 ns |  1.58 |    0.02 | 0.0100 |   5,911 B |      44 B |
    //|     ConcurrentTLru | 169.7 ns | 0.86 ns | 0.80 ns |  1.52 |    0.02 | 0.0098 |   5,982 B |      43 B |
#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [SimpleJob(RuntimeMoniker.Net48)]
#endif
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class LruZipDistribution
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
