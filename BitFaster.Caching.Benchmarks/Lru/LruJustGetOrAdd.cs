using BenchmarkDotNet.Attributes;
using BitFaster.Caching;
using BitFaster.Caching.Benchmarks.Lru;
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
    //|     ConcurrentDictionary |   7.730 ns | 0.0457 ns | 0.0427 ns |  1.00 |    0.00 |     340 B |      - |         - |
    //|        FastConcurrentLru |   9.524 ns | 0.0147 ns | 0.0122 ns |  1.23 |    0.01 |     427 B |      - |         - |
    //|            ConcurrentLru |  13.872 ns | 0.2476 ns | 0.2195 ns |  1.79 |    0.03 |     453 B |      - |         - |
    //|       FastConcurrentTLru |  25.819 ns | 0.1336 ns | 0.1250 ns |  3.34 |    0.03 |     613 B |      - |         - |
    //|           ConcurrentTLru |  30.176 ns | 0.5099 ns | 0.4520 ns |  3.90 |    0.06 |     688 B |      - |         - |
    //|               ClassicLru |  48.357 ns | 0.6203 ns | 0.5802 ns |  6.26 |    0.08 |     738 B |      - |         - |
    //|    RuntimeMemoryCacheGet | 106.552 ns | 0.5145 ns | 0.4561 ns | 13.78 |    0.09 |      49 B | 0.0074 |      32 B |
    //| ExtensionsMemoryCacheGet |  97.458 ns | 1.2148 ns | 1.1363 ns | 12.61 |    0.14 |      78 B | 0.0055 |      24 B |
    [DisassemblyDiagnoser(printSource: true)]
    [MemoryDiagnoser]
    public class LruJustGetOrAdd
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new(8, 9, EqualityComparer<int>.Default);

        private static readonly ClassicLru<int, int> classicLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

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

        [Benchmark()]
        public void FastConcurrentLru()
        {
            Func<int, int> func = x => x;
            fastConcurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLru()
        {
            Func<int, int> func = x => x;
            concurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void FastConcurrentTLru()
        {
            Func<int, int> func = x => x;
            fastConcurrentTLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentTLru()
        {
            Func<int, int> func = x => x;
            concurrentTlru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ClassicLru()
        {
            Func<int, int> func = x => x;
            classicLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void RuntimeMemoryCacheGet()
        {
            memoryCache.Get("1");
        }

        [Benchmark()]
        public void ExtensionsMemoryCacheGet()
        {
            exMemoryCache.Get(1);
        }

        public class MemoryCacheOptionsAccessor
            : Microsoft.Extensions.Options.IOptions<MemoryCacheOptions>
        {
            private readonly MemoryCacheOptions options = new MemoryCacheOptions();

            public MemoryCacheOptions Value => this.options;

        }
    }
}
