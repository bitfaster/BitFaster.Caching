using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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


    //|             Method |     Mean |    Error |   StdDev | Ratio | Code Size |  Gen 0 | Allocated |
    //|------------------- |---------:|---------:|---------:|------:|----------:|-------:|----------:|
    //|  FastConcurrentLru | 22.86 us | 0.183 us | 0.162 us |  1.00 |      5 KB | 2.1362 |      9 KB |
    //|      ConcurrentLru | 23.40 us | 0.092 us | 0.077 us |  1.02 |      5 KB | 2.1362 |      9 KB |
    //| ConcurrentLruEvent | 24.23 us | 0.097 us | 0.086 us |  1.06 |      5 KB | 3.0823 |     13 KB |
    //| FastConcurrentTLru | 31.70 us | 0.087 us | 0.077 us |  1.39 |      6 KB | 2.3193 |     10 KB |
    //|     ConcurrentTLru | 31.85 us | 0.080 us | 0.071 us |  1.39 |      6 KB | 2.3193 |     10 KB |
    //|         ClassicLru | 16.35 us | 0.091 us | 0.076 us |  0.72 |      4 KB | 3.2959 |     14 KB |
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Median", "RatioSD")]
    public class LruCycleBench
    {
        private static readonly ClassicLru<int, int> classicLru = new ClassicLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLruEvent = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new ConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new FastConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new FastConcurrentTLru<int, int>(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        [GlobalSetup]
        public void GlobalSetup()
        {
            concurrentLruEvent.Events.Value.ItemRemoved += OnItemRemoved;
        }

        public static int field;

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void OnItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            field = e.Key;
        }

        [Benchmark(Baseline =true)]
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
        public void ConcurrentLruEvent()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 128; i++)
                concurrentLruEvent.GetOrAdd(i, func);
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
