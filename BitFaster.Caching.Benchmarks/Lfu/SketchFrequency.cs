
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lfu;

namespace BitFaster.Caching.Benchmarks.Lfu
{
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class SketchFrequency
    {
        const int iterations = 512;
        private static CmSketch<int, Disable> std = new CmSketch<int, Disable>(10, EqualityComparer<int>.Default);
        private static CmSketch<int, Detect> avx = new CmSketch<int, Detect>(10, EqualityComparer<int>.Default);

        [GlobalSetup]
        public void Setup()
        {
            for (int i = 0; i < iterations; i++)
            {
                if (i % 3 == 0)
                {
                    std.Increment(i);
                    avx.Increment(i);
                }
            }
        }

        [Benchmark(Baseline = true)]
        public bool EstimateFrequency()
        {
            return std.EstimateFrequency(1) > std.EstimateFrequency(2);
        }

        [Benchmark()]
        public bool EstimateFrequencyAvx()
        {
            return avx.EstimateFrequency(1) > avx.EstimateFrequency(2);
        }
    }
}
