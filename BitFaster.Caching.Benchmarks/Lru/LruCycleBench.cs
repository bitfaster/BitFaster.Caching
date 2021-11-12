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


    //|             Method |     Mean |    Error |   StdDev | Ratio | Code Size |  Gen 0 | Allocated |
    //|------------------- |---------:|---------:|---------:|------:|----------:|-------:|----------:|
    //|  FastConcurrentLru | 22.99 us | 0.048 us | 0.037 us |  1.00 |      0 KB | 2.1362 |      9 KB |
    //|      ConcurrentLru | 23.48 us | 0.107 us | 0.100 us |  1.02 |      0 KB | 2.1362 |      9 KB |
    //| ConcurrentLruEvent | 24.54 us | 0.098 us | 0.087 us |  1.07 |      0 KB | 4.2725 |     18 KB |
    //| FastConcurrentTLru | 30.99 us | 0.068 us | 0.056 us |  1.35 |      1 KB | 2.3193 |     10 KB |
    //|     ConcurrentTLru | 32.53 us | 0.247 us | 0.219 us |  1.41 |      1 KB | 2.3193 |     10 KB |
    //|         ClassicLru | 16.15 us | 0.034 us | 0.029 us |  0.70 |      1 KB | 3.2959 |     14 KB |
    [DisassemblyDiagnoser(printSource: true)]
    [MemoryDiagnoser]
    public class LruCycleBench
    {
        private static readonly ClassicLru<int, int> classicLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentLru<int, int> concurrentLruEvent = new(8, 9, EqualityComparer<int>.Default);
        private static readonly ConcurrentTLru<int, int> concurrentTlru = new(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(10));
        private static readonly FastConcurrentLru<int, int> fastConcurrentLru = new(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentTLru<int, int> fastConcurrentTLru = new(8, 9, EqualityComparer<int>.Default, TimeSpan.FromMinutes(1));

        [GlobalSetup]
        public void GlobalSetup()
        {
            concurrentLruEvent.ItemRemoved += OnItemRemoved;
        }

        private int field;

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
