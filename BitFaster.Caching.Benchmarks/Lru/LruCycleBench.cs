using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks.Lru
{
    //BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
    //Intel Xeon W-2133 CPU 3.60GHz, 1 CPU, 12 logical and 6 physical cores
    //.NET SDK= 6.0.100
    //  [Host]     : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
    //  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT


    //|             Method |     Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 | Code Size | Allocated |
    //|------------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|----------:|
    //|  FastConcurrentLru | 23.25 us | 0.128 us | 0.114 us |  1.00 |    0.00 | 2.1362 |      5 KB |      9 KB |
    //|      ConcurrentLru | 23.78 us | 0.116 us | 0.097 us |  1.02 |    0.01 | 2.1362 |      5 KB |      9 KB |
    //| FastConcurrentTLru | 32.17 us | 0.463 us | 0.433 us |  1.38 |    0.02 | 2.3193 |      6 KB |     10 KB |
    //|     ConcurrentTLru | 32.52 us | 0.386 us | 0.361 us |  1.40 |    0.02 | 2.3193 |      6 KB |     10 KB |
    //|         ClassicLru | 16.29 us | 0.195 us | 0.163 us |  0.70 |    0.01 | 3.2959 |      5 KB |     14 KB |
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser]
    public class LruCycleBench
    {
        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new ConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

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
