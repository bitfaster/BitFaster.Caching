
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lfu;

namespace BitFaster.Caching.Benchmarks.Lfu
{
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class SketchIncrement
    {
        const int iterations = 1024;
        private static CmSketch<int, DisableAvx2> std = new CmSketch<int, DisableAvx2>(10, EqualityComparer<int>.Default);
        private static CmSketch<int, DetectAvx2> avx = new CmSketch<int, DetectAvx2>(10, EqualityComparer<int>.Default);

        [Benchmark(Baseline = true)]
        public void Inc()
        {
            for (int i = 0; i < iterations; i++)
            {
                std.Increment(i);
            }
        }

        [Benchmark()]
        public void IncAvx()
        {
            for (int i = 0; i < iterations; i++)
            {
                avx.Increment(i);
            }
        }
    }
}
