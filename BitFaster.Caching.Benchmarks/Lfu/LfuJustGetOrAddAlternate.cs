
using System;
using System.Collections.Generic;
using Benchly;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Benchmarks
{
    // Note: to run in VS, make .net9 the first target fmk in the .vcproj
#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
#endif
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    [ColumnChart(Title = "Lookup Latency ({JOB})", Output = OutputMode.PerJob, Colors = "darkslategray,royalblue,royalblue,royalblue,royalblue,royalblue,royalblue,royalblue,#ffbf00,limegreen,indianred,indianred")]
    public class LfuJustGetOrAddAlternate
    {
        const int stripes = 1;
        private static readonly BackgroundThreadScheduler background = new BackgroundThreadScheduler();
        private static readonly ConcurrentLfu<string, int> concurrentLfu = new ConcurrentLfu<string, int>(stripes, 9, background, EqualityComparer<string>.Default);

        [Benchmark(Baseline = true)]
        public int ConcurrentLfu()
        {
            Func<string, int> func = x => 1;
            return concurrentLfu.GetOrAdd("foo", func);
        }

#if NET9_0_OR_GREATER
        private static readonly AlternateLookup<ReadOnlySpan<char>, string, int> alternate = concurrentLfu.GetAlternateLookup<ReadOnlySpan<char>>();

        [Benchmark()]
        public int ConcurrentLfuAlternate()
        {
            Func<string, int> func = x => 1;
            return alternate.GetOrAdd("foo".AsSpan(), func);
        }
#endif
    }
}
