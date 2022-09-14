
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
        private static CmSketch<int, DisableAvx2> std = new CmSketch<int, DisableAvx2>(10, EqualityComparer<int>.Default);
        private static CmSketch<int, DetectAvx2> avx = new CmSketch<int, DetectAvx2>(10, EqualityComparer<int>.Default);

        [GlobalSetup]
        public void Setup()
        {
            for (int i = 0; i < 128; i++)
            {
                if (i % 3 == 0)
                {
                    std.Increment(i);
                    avx.Increment(i);
                }
            }
        }

        [Benchmark(Baseline = true)]
        public int EstimateFrequency()
        {
            int count = 0;
            for (int i = 0; i < 128; i++)
            {
                if (std.EstimateFrequency(i) > std.EstimateFrequency(i + 1))
                    count++;
            }

            return count;
        }

        [Benchmark()]
        public int EstimateFrequencyAvx()
        {
            int count = 0;
            for (int i = 0; i < 128; i++)
            {
                if (avx.EstimateFrequency(i) > avx.EstimateFrequency(i + 1))
                    count++;
            }

            return count;
        }
    }
}
