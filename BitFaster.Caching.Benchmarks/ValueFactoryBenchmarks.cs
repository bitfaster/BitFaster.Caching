
using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace BitFaster.Caching.Benchmarks
{
#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 3)]
    [SimpleJob(RuntimeMoniker.Net48)]
#endif
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class ValueFactoryBenchmarks
    {
        [Benchmark(Baseline = true)]
        public int Delegate()
        {
            Func<int, int> valueFactory = (k) => k;
            return valueFactory(1);
        }

        [Benchmark()]
        public int ValueFactory()
        {
            var valueFactory = new ValueFactory<int, int>(i => i);
            return Invoke<int, int, ValueFactory<int, int>>(valueFactory, 1);
        }

        [Benchmark()]
        public int ValueFactoryRef()
        {
            var valueFactory = new ValueFactory<int, int>(i => i);
            return InvokeRef<int, int, ValueFactory<int, int>>(ref valueFactory, 1);
        }

        private V Invoke<K, V, TFactory>(TFactory factory, K key) where TFactory : struct, IValueFactory<K, V>
        {
            return factory.Create(key);
        }

        private V InvokeRef<K, V, TFactory>(ref TFactory factory, K key) where TFactory : struct, IValueFactory<K, V>
        {
            return factory.Create(key);
        }
    }


#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 3)]
    [SimpleJob(RuntimeMoniker.Net48)]
#endif
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class ValueFactoryArgBenchmarks
    {
        [Benchmark(Baseline = true)]
        public int Delegate()
        {
            Func<int, int, int> valueFactory = (k, v) => k + v;
            return valueFactory(1, 2);
        }

        [Benchmark()]
        public int ValueFactory()
        {
            var valueFactory = new ValueFactoryArg<int,int, int>((k, v) => k + v, 2);
            return Invoke<int, int, ValueFactoryArg<int, int, int>>(valueFactory, 1);
        }

        [Benchmark()]
        public int ValueFactoryRef()
        {
            var valueFactory = new ValueFactoryArg<int, int, int>((k, v) => k + v, 2);
            return InvokeRef<int, int, ValueFactoryArg<int, int, int>>(ref valueFactory, 1);
        }

        private V Invoke<K, V, TFactory>(TFactory factory, K key) where TFactory : struct, IValueFactory<K, V>
        {
            return factory.Create(key);
        }

        private V InvokeRef<K, V, TFactory>(ref TFactory factory, K key) where TFactory : struct, IValueFactory<K, V>
        {
            return factory.Create(key);
        }
    }

#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 3)]
    [SimpleJob(RuntimeMoniker.Net48)]
#endif
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class ValueFactoryBigArgBenchmarks
    {
        [Benchmark(Baseline = true)]
        public int Delegate()
        {
            Func<int, ValueTuple<long, long, long>, int> valueFactory = (k, v) => k;
            return valueFactory(1, (0, 1, 2));
        }

        [Benchmark()]
        public int ValueFactory()
        {
            var valueFactory = new ValueFactoryArg<int, ValueTuple<long, long, long>, int>((k, v) => k , (0, 1, 2));
            return Invoke<int, int, ValueFactoryArg<int, ValueTuple<long, long, long>, int>>(valueFactory, 1);
        }

        [Benchmark()]
        public int ValueFactoryRef()
        {
            var valueFactory = new ValueFactoryArg<int, ValueTuple<long, long, long>, int>((k, v) => k, (0, 1, 2));
            return InvokeRef<int, int, ValueFactoryArg<int, ValueTuple<long, long, long>, int>>(ref valueFactory, 1);
        }

        private V Invoke<K, V, TFactory>(TFactory factory, K key) where TFactory : struct, IValueFactory<K, V>
        {
            return factory.Create(key);
        }

        private V InvokeRef<K, V, TFactory>(ref TFactory factory, K key) where TFactory : struct, IValueFactory<K, V>
        {
            return factory.Create(key);
        }
    }
}
