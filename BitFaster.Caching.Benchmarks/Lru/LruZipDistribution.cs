using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;

namespace BitFaster.Caching.Benchmarks.Lru
{
    //BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
    //Intel Xeon W-2133 CPU 3.60GHz, 1 CPU, 12 logical and 6 physical cores
    //.NET SDK= 6.0.100
    //  [Host]     : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
    //  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT


    //|             Method |     Mean |   Error |  StdDev | Ratio | RatioSD |  Gen 0 | Code Size | Allocated |
    //|------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|----------:|
    //|         ClassicLru | 108.4 ns | 0.26 ns | 0.20 ns |  1.00 |    0.00 | 0.0154 |     799 B |      67 B |
    //|  FastConcurrentLru | 123.1 ns | 0.97 ns | 0.86 ns |  1.14 |    0.01 | 0.0093 |     488 B |      41 B |
    //|      ConcurrentLru | 128.7 ns | 2.12 ns | 1.98 ns |  1.19 |    0.02 | 0.0093 |     510 B |      40 B |
    //| FastConcurrentTLru | 166.1 ns | 0.99 ns | 0.83 ns |  1.53 |    0.01 | 0.0100 |     674 B |      43 B |
    //|     ConcurrentTLru | 172.2 ns | 0.52 ns | 0.46 ns |  1.59 |    0.00 | 0.0103 |     745 B |      45 B |
    [DisassemblyDiagnoser(printSource: true)]
    [MemoryDiagnoser]
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
