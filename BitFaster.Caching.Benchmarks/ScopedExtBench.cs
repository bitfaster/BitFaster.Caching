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
    //|                                  Method |       Mean |     Error |    StdDev | Ratio | RatioSD | Code Size |  Gen 0 | Allocated |
    //|---------------------------------------- |-----------:|----------:|----------:|------:|--------:|----------:|-------:|----------:|
    //|                    ConcurrentDictionary |   8.791 ns | 0.0537 ns | 0.0476 ns |  0.48 |    0.00 |     396 B |      - |         - |
    //|                           ConcurrentLru |  18.429 ns | 0.1539 ns | 0.1440 ns |  1.00 |    0.00 |     701 B |      - |         - |
    //|           ScopedConcurrentLruNativeFunc | 117.665 ns | 1.4390 ns | 1.3461 ns |  6.39 |    0.10 |     662 B | 0.0389 |     168 B |
    //|          ScopedConcurrentLruWrappedFunc | 132.697 ns | 0.6867 ns | 0.5734 ns |  7.19 |    0.08 |     565 B | 0.0610 |     264 B |
    //| ScopedConcurrentLruWrappedFuncProtected | 133.997 ns | 0.5089 ns | 0.4249 ns |  7.26 |    0.05 |     621 B | 0.0610 |     264 B |
    [DisassemblyDiagnoser(printSource: true)]
    [MemoryDiagnoser]
    public class ScopedExtBench
    {
        private static readonly ConcurrentDictionary<int, SomeDisposable> dictionary = new ConcurrentDictionary<int, SomeDisposable>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, SomeDisposable> concurrentLru = new ConcurrentLru<int, SomeDisposable>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, Scoped<SomeDisposable>> scopedConcurrentLru = new ConcurrentLru<int, Scoped<SomeDisposable>>(8, 9, EqualityComparer<int>.Default);

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
        public SomeDisposable ScopedConcurrentLruNativeFunc()
        {
            // function generates actual cached object (scoped wrapping item)
            Func<int, Scoped<SomeDisposable>> func = x => new Scoped<SomeDisposable>(new SomeDisposable());
            using (var l = scopedConcurrentLru.ScopedGetOrAdd(1, func))
            {
                return l.Value;
            }
        }

        [Benchmark()]
        public SomeDisposable ScopedConcurrentLruWrappedFunc()
        {
            // function generates item, extension method allocates a closure to create scoped<item>
            Func<int, SomeDisposable> func = x => new SomeDisposable();
            using (var l = scopedConcurrentLru.ScopedGetOrAdd(1, func))
            {
                return l.Value;
            }
        }

        [Benchmark()]
        public SomeDisposable ScopedConcurrentLruWrappedFuncProtected()
        {
            // function generates item, extension method allocates a closure to create scoped<item>
            Func<int, SomeDisposable> func = x => new SomeDisposable();
            using (var l = scopedConcurrentLru.ScopedGetOrAddProtected(1, func))
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
