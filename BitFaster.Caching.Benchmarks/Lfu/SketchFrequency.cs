
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
        const int sketchSize = 1_048_576;
        const int iterations = 1_048_576;
        
        private static CmSketch<int, DisableHardwareIntrinsics> std = new CmSketch<int, DisableHardwareIntrinsics>(sketchSize, EqualityComparer<int>.Default);
        private static CmSketch<int, DetectIsa> avx = new CmSketch<int, DetectIsa>(sketchSize, EqualityComparer<int>.Default);

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

        [Benchmark(Baseline = true, OperationsPerInvoke = iterations)]
        public int EstimateFrequency()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += std.EstimateFrequency(i) > std.EstimateFrequency(i + 1) ? 1: 0;

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public int EstimateFrequencyAvx()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += avx.EstimateFrequency(i) > avx.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }
    }
}
