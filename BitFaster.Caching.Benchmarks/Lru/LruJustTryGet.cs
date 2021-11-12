using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks.Lru
{
    //BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
    //Intel Xeon W-2133 CPU 3.60GHz, 1 CPU, 12 logical and 6 physical cores
    //.NET SDK= 6.0.100
    //  [Host]     : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
    //  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT


    //|               Method |      Mean |     Error |    StdDev | Ratio | RatioSD | Code Size | Allocated |
    //|--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|
    //| ConcurrentDictionary |  4.480 ns | 0.0230 ns | 0.0204 ns |  1.00 |    0.00 |     364 B |         - |
    //|    FastConcurrentLru |  7.705 ns | 0.0343 ns | 0.0286 ns |  1.72 |    0.01 |     448 B |         - |
    //|   FastConcurrentTLru | 25.350 ns | 0.3301 ns | 0.3088 ns |  5.66 |    0.08 |     546 B |         - |
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser]
    public class LruJustTryGet
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));


        [GlobalSetup]
        public void GlobalSetup()
        {
            dictionary.TryAdd(1, 1);
            fastConcurrentLru.GetOrAdd(1, k => k);
            fastConcurrentTLru.GetOrAdd(1, k => k);
        }

        [Benchmark(Baseline = true)]
        public int ConcurrentDictionary()
        {
            dictionary.TryGetValue(1, out var value);
            return value;
        }

        [Benchmark()]
        public int FastConcurrentLru()
        {
            fastConcurrentLru.TryGet(1, out var value);
            return value;
        }

        [Benchmark()]
        public int FastConcurrentTLru()
        {
            fastConcurrentTLru.TryGet(1, out var value);
            return value;
        }
    }
}
