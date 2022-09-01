using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching;
using BitFaster.Caching.Benchmarks.Lru;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    //[DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser]
    // [HardwareCounters(HardwareCounter.LlcMisses, HardwareCounter.CacheMisses)] // Requires Admin https://adamsitnik.com/Hardware-Counters-Diagnoser/
    // [ThreadingDiagnoser] // Requires .NET Core
    public class LfuJustGetOrAdd
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        const int stripes = 1;
        private static readonly BackgroundThreadScheduler background = new BackgroundThreadScheduler();
        private static readonly ConcurrentLfu<int, int> concurrentLfu = new ConcurrentLfu<int, int>(stripes, 9, background, EqualityComparer<int>.Default, BufferConfiguration.CreateDefault(1, 128));

        private static readonly ConcurrentLfu<int, int> concurrentLfuFore = new ConcurrentLfu<int, int>(stripes, 9, new ForegroundScheduler(), EqualityComparer<int>.Default, BufferConfiguration.CreateDefault(1, 128));
        private static readonly ConcurrentLfu<int, int> concurrentLfuTp = new ConcurrentLfu<int, int>(stripes, 9, new ThreadPoolScheduler(), EqualityComparer<int>.Default, BufferConfiguration.CreateDefault(1, 128));
        private static readonly ConcurrentLfu<int, int> concurrentLfuNull = new ConcurrentLfu<int, int>(stripes, 9, new NullScheduler(), EqualityComparer<int>.Default, BufferConfiguration.CreateDefault(1, 128));

        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
           background.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void ConcurrentDictionary()
        {
            Func<int, int> func = x => x;
            dictionary.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLfuBackground()
        {
            Func<int, int> func = x => x;
            concurrentLfu.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLfuForeround()
        {
            Func<int, int> func = x => x;
            concurrentLfuFore.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLfuThreadPool()
        {
            Func<int, int> func = x => x;
            concurrentLfuTp.GetOrAdd(1, func);
        }

        [Benchmark()]
        public void ConcurrentLfuNull()
        {
            Func<int, int> func = x => x;
            concurrentLfuNull.GetOrAdd(1, func);
        }
    }
}
