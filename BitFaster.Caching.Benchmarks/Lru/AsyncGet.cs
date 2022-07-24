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
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser]
    public class AsyncGet
    {
        private static readonly IAsyncCache<int, int> concurrentLru = new ConcurrentLruBuilder<int, int>().AsAsyncCache().Build();
        private static readonly IAsyncCache<int, int> atomicConcurrentLru = new ConcurrentLruBuilder<int, int>().AsAsyncCache().WithAtomicCreate().Build();

        private static Task<int> returnTask = Task.FromResult(1);

        private static int[] data = new int[128];

        [Benchmark()]
        public async Task GetOrAddAsync()
        {
            Func<int, Task<int>> func = x => returnTask;

            for (int i = 0; i < 128; i++)
                data[i] = await concurrentLru.GetOrAddAsync(i, func);
        }

        [Benchmark()]
        public async Task AtomicGetOrAddAsync()
        {
            Func<int, Task<int>> func = x => returnTask;

            for (int i = 0; i < 128; i++)
                data[i] = await atomicConcurrentLru.GetOrAddAsync(i, func);
        }
    }
}
