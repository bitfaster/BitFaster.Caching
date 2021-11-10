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
    [DisassemblyDiagnoser(printSource: true)]
    [MemoryDiagnoser]
    public class AtomicBench
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, Atomic<int, int>> atomicConcurrentLru = new ConcurrentLru<int, Atomic<int, int>>(8, 9, EqualityComparer<int>.Default);

        [Benchmark()]
        public void ConcurrentDictionary()
        {
            Func<int, int> func = x => x;
            dictionary.GetOrAdd(1, func);
        }

        [Benchmark(Baseline = true)]
        public void ConcurrentLru()
        {
            Func<int, int> func = x => x;
            concurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void AtomicConcurrentLru()
        {
            Func<int, int> func = x => x;
            atomicConcurrentLru.GetOrAdd(1, func);
        }
    }
}
