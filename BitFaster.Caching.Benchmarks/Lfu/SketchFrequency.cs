
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
        private static CmSketchBlock<int, DisableHardwareIntrinsics> block = new CmSketchBlock<int, DisableHardwareIntrinsics>(sketchSize, EqualityComparer<int>.Default);
        private static CmSketchBlock<int, DetectIsa> blockAvx = new CmSketchBlock<int, DetectIsa>(sketchSize, EqualityComparer<int>.Default);

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

        [Benchmark(OperationsPerInvoke = iterations)]
        public int CompoundEstimateFrequencyAvx()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
            {
                var (a, b) = avx.EstimateFrequency(i, i + 1);
                count += a;
            }

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public int EstimateFrequencyBlock()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += block.EstimateFrequency(i) > block.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public int EstimateFrequencyBlockAvx()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += blockAvx.EstimateFrequency(i) > blockAvx.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public int CompoundEstimateFrequencyBlockAvx()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
            {
                var (a, b) = blockAvx.EstimateFrequency(i, i + 1);
                count += a;
            }

            return count;
        }
    }
}
