using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BitFaster.Caching.Lazy;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks
{
    [DisassemblyDiagnoser(printSource: true)]
    [MemoryDiagnoser]
    public class AsyncAtomicBench
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, int> concurrentLru = new ConcurrentLru<int, int>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, AsyncAtomic<int, int>> atomicConcurrentLru = new ConcurrentLru<int, AsyncAtomic<int, int>>(8, 9, EqualityComparer<int>.Default);

        [Benchmark()]
        public void ConcurrentDictionary()
        {
            Func<int, int> func = x => x;
            dictionary.GetOrAdd(1, func);
        }

        [Benchmark(Baseline = true)]
        public async Task ConcurrentLruAsync()
        {
            Func<int, Task<int>> func = x => Task.FromResult(x);
            await concurrentLru.GetOrAddAsync(1, func).ConfigureAwait(false);
        }

        [Benchmark()]
        public async Task AtomicConcurrentLruAsync()
        {
           Func<int, Task<int>> func = x => Task.FromResult(x);
           await atomicConcurrentLru.GetOrAddAsync(1, func).ConfigureAwait(false);
        }
    }
}
