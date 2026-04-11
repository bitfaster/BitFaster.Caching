
using System;
using System.Collections.Generic;
using Benchly;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Benchmarks
{
    // Note: to run in VS, make .net9 the first target fmk in the .vcproj
#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
#endif
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    [ColumnChart(Title = "Lookup Latency ({JOB})", Output = OutputMode.PerJob, Colors = "darkslategray,royalblue,royalblue,royalblue,royalblue,royalblue,royalblue,royalblue,#ffbf00,limegreen,indianred,indianred")]
    public class LruJustGetOrAddAlternate
    {
        private static readonly ConcurrentLru<string, int> concurrentLru = new ConcurrentLru<string, int>(8, 9, EqualityComparer<string>.Default);

        [Benchmark(Baseline = true)]
        public int ConcurrentLru()
        {
            Func<string, int> func = x => 1;
            return concurrentLru.GetOrAdd("foo", func);
        }

#if NET9_0_OR_GREATER
        private static readonly IAlternateLookup<ReadOnlySpan<char>, string, int> alternate = concurrentLru.GetAlternateLookup<ReadOnlySpan<char>>();

        [Benchmark()]
        public int ConcurrentLruAlternate()
        {
            Func<string, int> func = x => 1;
            return alternate.GetOrAdd("foo".AsSpan(), func);
        }
#endif
    }
}
