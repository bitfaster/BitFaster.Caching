using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching;
using BitFaster.Caching.Benchmarks.Lru;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Benchmarks
{
    //BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
    //Intel Xeon W-2133 CPU 3.60GHz, 1 CPU, 12 logical and 6 physical cores
    //.NET SDK= 6.0.100
    //  [Host]     : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
    //  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT


    //|                   Method |       Mean |     Error |    StdDev | Ratio | RatioSD | Code Size |  Gen 0 | Allocated |
    //|------------------------- |-----------:|----------:|----------:|------:|--------:|----------:|-------:|----------:|
    //|     ConcurrentDictionary |   7.868 ns | 0.0543 ns | 0.0481 ns |  1.00 |    0.00 |   1,523 B |      - |         - |
    //|        FastConcurrentLru |  10.340 ns | 0.0496 ns | 0.0464 ns |  1.31 |    0.01 |   2,185 B |      - |         - |
    //|            ConcurrentLru |  13.739 ns | 0.0979 ns | 0.0916 ns |  1.75 |    0.01 |   2,207 B |      - |         - |
    //|       FastConcurrentTLru |  25.820 ns | 0.0933 ns | 0.0729 ns |  3.28 |    0.02 |   2,371 B |      - |         - |
    //|           ConcurrentTLru |  29.732 ns | 0.1387 ns | 0.1229 ns |  3.78 |    0.03 |   2,442 B |      - |         - |
    //|               ClassicLru |  49.041 ns | 0.8575 ns | 0.8021 ns |  6.23 |    0.11 |   3,013 B |      - |         - |
    //|    RuntimeMemoryCacheGet | 107.769 ns | 1.1901 ns | 0.9938 ns | 13.69 |    0.15 |      49 B | 0.0074 |      32 B |
    //| ExtensionsMemoryCacheGet |  93.188 ns | 0.2321 ns | 0.2171 ns | 11.85 |    0.07 |      78 B | 0.0055 |      24 B |
    [SimpleJob(RuntimeMoniker.Net48, 2, 10, 15)]
    [SimpleJob(RuntimeMoniker.Net60, 2, 10, 15)]
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser]
    // [HardwareCounters(HardwareCounter.LlcMisses, HardwareCounter.CacheMisses)] // Requires Admin https://adamsitnik.com/Hardware-Counters-Diagnoser/
    // [ThreadingDiagnoser] // Requires .NET Core
    public class LruJustGetOrAdd
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new ConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        private static readonly ICache<int, int> atomicFastLru = new ConcurrentLruBuilder<int, int>().WithConcurrencyLevel(8).WithCapacity(9).WithAtomicGetOrAdd().Build();

        private static readonly ConcurrentLfu<int, int> concurrentLfu = new ConcurrentLfu<int, int>(9);

        private static readonly int key = 1;
        private static System.Runtime.Caching.MemoryCache memoryCache = System.Runtime.Caching.MemoryCache.Default;

        Microsoft.Extensions.Caching.Memory.MemoryCache exMemoryCache 
            = new Microsoft.Extensions.Caching.Memory.MemoryCache(new MemoryCacheOptionsAccessor());

        [GlobalSetup]
        public void GlobalSetup()
        {
            memoryCache.Set(key.ToString(), "test", new System.Runtime.Caching.CacheItemPolicy());
            exMemoryCache.Set(key, "test");
        }

        [Benchmark(Baseline = true)]
        public void ConcurrentDictionary()
        {
            Func<int, int> func = x => x;
            dictionary.GetOrAdd(1, func);
        }

        //[Benchmark()]
        //public void FastConcurrentLru()
        //{
        //    Func<int, int> func = x => x;
        //    fastConcurrentLru.GetOrAdd(1, func);
        //}

        //[Benchmark()]
        //public void ConcurrentLru()
        //{
        //    Func<int, int> func = x => x;
        //    concurrentLru.GetOrAdd(1, func);
        //}

        //[Benchmark()]
        //public void AtomicFastLru()
        //{
        //    Func<int, int> func = x => x;
        //    atomicFastLru.GetOrAdd(1, func);
        //}

        //[Benchmark()]
        //public void FastConcurrentTLru()
        //{
        //    Func<int, int> func = x => x;
        //    fastConcurrentTLru.GetOrAdd(1, func);
        //}

        //[Benchmark()]
        //public void ConcurrentTLru()
        //{
        //    Func<int, int> func = x => x;
        //    concurrentTlru.GetOrAdd(1, func);
        //}

        [Benchmark()]
        public void ConcurrentLfu()
        {
            Func<int, int> func = x => x;
            concurrentLfu.GetOrAdd(1, func);
        }

        //[Benchmark()]
        //public void ClassicLru()
        //{
        //    Func<int, int> func = x => x;
        //    classicLru.GetOrAdd(1, func);
        //}

        //[Benchmark()]
        //public void RuntimeMemoryCacheGet()
        //{
        //    memoryCache.Get("1");
        //}

        //[Benchmark()]
        //public void ExtensionsMemoryCacheGet()
        //{
        //    exMemoryCache.Get(1);
        //}

        public class MemoryCacheOptionsAccessor
            : Microsoft.Extensions.Options.IOptions<MemoryCacheOptions>
        {
            private readonly MemoryCacheOptions options = new MemoryCacheOptions();

            public MemoryCacheOptions Value => this.options;

        }
    }
}
