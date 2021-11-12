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
    //|  FastConcurrentLru | 22.74 us | 0.083 us | 0.069 us |  1.00 |      0 KB | 2.1362 |      9 KB |
    //|      ConcurrentLru | 24.02 us | 0.097 us | 0.086 us |  1.06 |      0 KB | 2.1362 |      9 KB |
    //| ConcurrentLruEvent | 24.82 us | 0.117 us | 0.104 us |  1.09 |      0 KB | 4.2725 |     18 KB |
    //| FastConcurrentTLru | 31.38 us | 0.066 us | 0.058 us |  1.38 |      1 KB | 2.3193 |     10 KB |
    //|     ConcurrentTLru | 32.03 us | 0.175 us | 0.147 us |  1.41 |      1 KB | 2.3193 |     10 KB |
    //|         ClassicLru | 16.26 us | 0.146 us | 0.129 us |  0.72 |      1 KB | 3.2959 |     14 KB |
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
