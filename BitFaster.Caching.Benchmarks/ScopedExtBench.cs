using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks
{
    //|               Method |       Mean |     Error |    StdDev | Ratio | RatioSD | Code Size |  Gen 0 | Allocated |
    //|--------------------- |-----------:|----------:|----------:|------:|--------:|----------:|-------:|----------:|
    //| ConcurrentDictionary |   8.453 ns | 0.0445 ns | 0.0394 ns |  0.46 |    0.00 |     396 B |      - |         - |
    //|        ConcurrentLru |  18.405 ns | 0.1529 ns | 0.1277 ns |  1.00 |    0.00 |     701 B |      - |         - |
    //|  ScopedConcurrentLru | 115.748 ns | 0.5271 ns | 0.4673 ns |  6.29 |    0.04 |     662 B | 0.0389 |     168 B |
    //| ScopedConcurrentLru2 | 134.296 ns | 0.9543 ns | 0.8927 ns |  7.30 |    0.07 |     565 B | 0.0610 |     264 B |
    [DisassemblyDiagnoser(printSource: true)]
    [MemoryDiagnoser]
    public class ScopedExtBench
    {
        private static readonly ConcurrentDictionary<int, SomeDisposable> dictionary = new ConcurrentDictionary<int, SomeDisposable>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, SomeDisposable> concurrentLru = new ConcurrentLru<int, SomeDisposable>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, Scoped<SomeDisposable>> scopedConcurrentLru = new ConcurrentLru<int, Scoped<SomeDisposable>>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, Scoped<SomeDisposable>> scopedConcurrentLru2 = new ConcurrentLru<int, Scoped<SomeDisposable>>(8, 9, EqualityComparer<int>.Default);

        [Benchmark()]
        public SomeDisposable ConcurrentDictionary()
        {
            Func<int, SomeDisposable> func = x => new SomeDisposable();
            return dictionary.GetOrAdd(1, func);
        }

        [Benchmark(Baseline = true)]
        public SomeDisposable ConcurrentLru()
        {
            Func<int, SomeDisposable> func = x => new SomeDisposable();
            return concurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public SomeDisposable ScopedConcurrentLru()
        {
            Func<int, Scoped<SomeDisposable>> func = x => new Scoped<SomeDisposable>(new SomeDisposable());
            using (var l = scopedConcurrentLru.ScopedGetOrAdd(1, func))
            {
                return l.Value;
            }
        }

        [Benchmark()]
        public SomeDisposable ScopedConcurrentLru2()
        {
            Func<int, SomeDisposable> func = x => new SomeDisposable();
            using (var l = scopedConcurrentLru.ScopedGetOrAdd2(1, func))
            {
                return l.Value;
            }
        }
    }

    public class SomeDisposable : IDisposable
    {
        public void Dispose()
        {

        }
    }
}
