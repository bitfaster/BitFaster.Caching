using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks.Lru
{
    /// <summary>
    /// Verify 0 allocs for GetOrAddAsync cache hits.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    // [DisassemblyDiagnoser(printSource: true, maxDepth: 5)] // Unstable
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Median", "RatioSD")]
    public class LruAsyncGet
    {
        // if the cache value is a value type, value task has no effect - so use string to repro.
        private static readonly IAsyncCache<int, string> concurrentLru = new ConcurrentLruBuilder<int, string>().AsAsyncCache().Build();
        private static readonly IAsyncCache<int, string> atomicConcurrentLru = new ConcurrentLruBuilder<int, string>().AsAsyncCache().WithAtomicGetOrAdd().Build();

        private static Task<string> returnTask = Task.FromResult("1");

        [Benchmark()]
        public async ValueTask<string> GetOrAddAsync()
        {
            Func<int, Task<string>> func = x => returnTask;

            return await concurrentLru.GetOrAddAsync(1, func);
        }

        [Benchmark()]
        public async ValueTask<string> AtomicGetOrAddAsync()
        {
            Func<int, Task<string>> func = x => returnTask;

            return await atomicConcurrentLru.GetOrAddAsync(1, func);
        }
    }
}
