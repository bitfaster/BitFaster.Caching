﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using Benchly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BitFaster.Caching.Benchmarks
{
#if Windows
    [SimpleJob(RuntimeMoniker.Net48)]
#endif
    [SimpleJob(RuntimeMoniker.Net60)]
    //[DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [MemoryDiagnoser(displayGenColumns: false)]
    // [HardwareCounters(HardwareCounter.LlcMisses, HardwareCounter.CacheMisses)] // Requires Admin https://adamsitnik.com/Hardware-Counters-Diagnoser/
    // [ThreadingDiagnoser] // Requires .NET Core
    [BoxPlot(Title = "LFU Read Latency")]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class LfuJustGetOrAdd
    {
        private static readonly ConcurrentDictionary<int, int> dictionary = new ConcurrentDictionary<int, int>(8, 9, EqualityComparer<int>.Default);

        const int stripes = 1;
        private static readonly BackgroundThreadScheduler background = new BackgroundThreadScheduler();
        private static readonly ConcurrentLfu<int, int> concurrentLfu = new ConcurrentLfu<int, int>(stripes, 9, background, EqualityComparer<int>.Default);

        private static readonly ConcurrentLfu<int, int> concurrentLfuFore = new ConcurrentLfu<int, int>(stripes, 9, new ForegroundScheduler(), EqualityComparer<int>.Default);
        private static readonly ConcurrentLfu<int, int> concurrentLfuTp = new ConcurrentLfu<int, int>(stripes, 9, new ThreadPoolScheduler(), EqualityComparer<int>.Default);
        private static readonly ConcurrentLfu<int, int> concurrentLfuNull = new ConcurrentLfu<int, int>(stripes, 9, new NullScheduler(), EqualityComparer<int>.Default);

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
        public int ConcurrentDictionary()
        {
            Func<int, int> func = x => x;
            return dictionary.GetOrAdd(1, func);
        }

        [Benchmark()]
        public int ConcurrentLfuBackground()
        {
            Func<int, int> func = x => x;
            return concurrentLfu.GetOrAdd(1, func);
        }

        [Benchmark()]
        public int ConcurrentLfuForeround()
        {
            Func<int, int> func = x => x;
            return concurrentLfuFore.GetOrAdd(1, func);
        }

        [Benchmark()]
        public int ConcurrentLfuThreadPool()
        {
            Func<int, int> func = x => x;
            return concurrentLfuTp.GetOrAdd(1, func);
        }

        [Benchmark()]
        public int ConcurrentLfuNull()
        {
            Func<int, int> func = x => x;
            return concurrentLfuNull.GetOrAdd(1, func);
        }
    }
}
