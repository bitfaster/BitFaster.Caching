using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Benchly;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;
using Microsoft.Extensions.Caching.Memory;

namespace BitFaster.Caching.Benchmarks
{

#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [SimpleJob(RuntimeMoniker.Net48)]
#endif
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    // [HardwareCounters(HardwareCounter.LlcMisses, HardwareCounter.CacheMisses)] // Requires Admin https://adamsitnik.com/Hardware-Counters-Diagnoser/
    // [ThreadingDiagnoser] // Requires .NET Core
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    [ColumnChart(Title = "Guid Lookup Latency ({JOB})", Output = OutputMode.PerJob, Colors = "darkslategray,royalblue,royalblue,#ffbf00,indianred,indianred")]
    public class LruJustGetOrAddGuid
    {
        private static readonly ConcurrentDictionary<int, Guid> dictionary = new ConcurrentDictionary<int, Guid>(8, 9, EqualityComparer<int>.Default);

        private static readonly ConcurrentLru<int, Guid> concurrentLru = new ConcurrentLru<int, Guid>(8, 9, EqualityComparer<int>.Default);
        private static readonly FastConcurrentLru<int, Guid> fastConcurrentLru = new FastConcurrentLru<int, Guid>(8, 9, EqualityComparer<int>.Default);


        private static readonly BackgroundThreadScheduler background = new BackgroundThreadScheduler();
        private static readonly ConcurrentLfu<int, Guid> concurrentLfu = new ConcurrentLfu<int, Guid>(1, 9, background, EqualityComparer<int>.Default);

        private static readonly int key = 1;
        private static System.Runtime.Caching.MemoryCache memoryCache = System.Runtime.Caching.MemoryCache.Default;

        Microsoft.Extensions.Caching.Memory.MemoryCache exMemoryCache
            = new Microsoft.Extensions.Caching.Memory.MemoryCache(new MemoryCacheOptionsAccessor());

        private static readonly byte[] b = new byte[8];

        [GlobalSetup]
        public void GlobalSetup()
        {
            memoryCache.Set(key.ToString(), new Guid(key, 0, 0, b), new System.Runtime.Caching.CacheItemPolicy());
            exMemoryCache.Set(key, new Guid(key, 0, 0, b));
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            background.Dispose();
        }

        [Benchmark(Baseline = true)]
        public Guid ConcurrentDictionary()
        {
            Func<int, Guid> func = x => new Guid(x, 0, 0, b);
            return dictionary.GetOrAdd(1, func);
        }

        [Benchmark()]
        public Guid FastConcurrentLru()
        {
            Func<int, Guid> func = x => new Guid(x, 0, 0, b);
            return fastConcurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public Guid ConcurrentLru()
        {
            Func<int, Guid> func = x => new Guid(x, 0, 0, b);
            return concurrentLru.GetOrAdd(1, func);
        }

        [Benchmark()]
        public Guid ConcurrentLfu()
        {
            Func<int, Guid> func = x => new Guid(x, 0, 0, b);
            return concurrentLfu.GetOrAdd(1, func);
        }

        [Benchmark()]
        public Guid RuntimeMemoryCacheGet()
        {
            return (Guid)memoryCache.Get("1");
        }

        [Benchmark()]
        public Guid ExtensionsMemoryCacheGet()
        {
            return (Guid)exMemoryCache.Get(1);
        }

        public class MemoryCacheOptionsAccessor
            : Microsoft.Extensions.Options.IOptions<MemoryCacheOptions>
        {
            private readonly MemoryCacheOptions options = new MemoryCacheOptions();

            public MemoryCacheOptions Value => this.options;

        }
    }
}
