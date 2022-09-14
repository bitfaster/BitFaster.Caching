using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace BitFaster.Caching.Benchmarks.Lru
{
    //BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
    //Intel Xeon W-2133 CPU 3.60GHz, 1 CPU, 12 logical and 6 physical cores
    //.NET SDK= 6.0.100
    //  [Host]     : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
    //  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT


    //|               Method |       Mean |     Error |    StdDev | Ratio | RatioSD | Code Size |  Gen 0 | Allocated |
    //|--------------------- |-----------:|----------:|----------:|------:|--------:|----------:|-------:|----------:|
    //| ConcurrentDictionary |   8.389 ns | 0.0233 ns | 0.0206 ns |  1.00 |    0.00 |   1,544 B |      - |         - |
    //|    FastConcurrentLru |  10.637 ns | 0.0808 ns | 0.0755 ns |  1.27 |    0.01 |   3,149 B |      - |         - |
    //|        ConcurrentLru |  13.977 ns | 0.0674 ns | 0.0526 ns |  1.67 |    0.01 |   3,171 B |      - |         - |
    //|   FastConcurrentTLru |  27.107 ns | 0.0810 ns | 0.0632 ns |  3.23 |    0.01 |   3,468 B |      - |         - |
    //|       ConcurrentTLru |  33.733 ns | 0.6613 ns | 0.6791 ns |  4.02 |    0.09 |   3,539 B |      - |         - |
    //|           ClassicLru |  52.898 ns | 0.3079 ns | 0.2404 ns |  6.30 |    0.03 |   3,021 B |      - |         - |
    //|          MemoryCache | 117.075 ns | 1.7664 ns | 1.5658 ns | 13.96 |    0.18 |      94 B | 0.0073 |      32 B |
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class LruMultiGet
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTLru = new ConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        private static MemoryCache memoryCache = System.Runtime.Caching.MemoryCache.Default;

        private static readonly string[] stringKeys = new string[6];

        [GlobalSetup]
        public void GlobalSetup()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 6; i++)
            {
                dictionary.GetOrAdd(1, func);

                concurrentLru.GetOrAdd(i, func);
                fastConcurrentLru.GetOrAdd(i, func);
                concurrentTLru.GetOrAdd(i, func);
                fastConcurrentTLru.GetOrAdd(i, func);

                classicLru.GetOrAdd(i, func);
                stringKeys[i] = i.ToString();
                memoryCache.Set(stringKeys[i], "test", new CacheItemPolicy());
            }
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = 24)]
        public void ConcurrentDictionary()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            { 
                for (int i = 0; i < 6; i++)
                {
                    dictionary.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void FastConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    fastConcurrentLru.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    concurrentLru.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void FastConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    fastConcurrentTLru.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    concurrentTLru.GetOrAdd(i, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void ClassicLru()
        {
            Func<int, int> func = x => x;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    classicLru.GetOrAdd(1, func);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 24)]
        public void MemoryCache()
        {
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    memoryCache.Get(stringKeys[i]);
                }
            }
        }
    }
}
