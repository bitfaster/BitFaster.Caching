using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks.Lru
{
    //BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
    //Intel Xeon W-2133 CPU 3.60GHz, 1 CPU, 12 logical and 6 physical cores
    //.NET SDK= 6.0.100
    //  [Host]     : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
    //  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT


    //|             Method |     Mean |    Error |   StdDev | Code Size |  Gen 0 | Allocated |
    //|------------------- |---------:|---------:|---------:|----------:|-------:|----------:|
    //|  FastConcurrentLru | 22.61 us | 0.125 us | 0.110 us |      0 KB | 2.1362 |      9 KB |
    //|      ConcurrentLru | 24.39 us | 0.389 us | 0.364 us |      0 KB | 2.1362 |      9 KB |
    //| FastConcurrentTLru | 31.28 us | 0.067 us | 0.062 us |      1 KB | 2.3193 |     10 KB |
    //|     ConcurrentTLru | 31.75 us | 0.074 us | 0.062 us |      1 KB | 2.3193 |     10 KB |
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser]
    public class LruCycleBench
    {
        private static readonly ClassicLru<int, int> classicLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        [Benchmark(Baseline = true)]
        public void FastConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                fastConcurrentLru.GetOrAdd(i, func);
        }

        [Benchmark()]
        public void ConcurrentLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                concurrentLru.GetOrAdd(i, func);
        }

        [Benchmark()]
        public void FastConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                fastConcurrentTLru.GetOrAdd(i, func);
        }

        [Benchmark()]
        public void ConcurrentTLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                concurrentTlru.GetOrAdd(i, func);
        }

        [Benchmark()]
        public void ClassicLru()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                classicLru.GetOrAdd(i, func);
        }
    }
}
