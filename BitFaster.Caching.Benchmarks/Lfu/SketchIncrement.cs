
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
        const int sketchSize = 1_048_576;
        const int iterations = 1_048_576;
        private static CmSketch<int, DisableHardwareIntrinsics> std = new CmSketch<int, DisableHardwareIntrinsics>(sketchSize, EqualityComparer<int>.Default);
        private static CmSketch<int, DetectIsa> avx = new CmSketch<int, DetectIsa>(sketchSize, EqualityComparer<int>.Default);

        private static CmSketchBlock<int, DisableHardwareIntrinsics> block = new CmSketchBlock<int, DisableHardwareIntrinsics>(sketchSize, EqualityComparer<int>.Default);
        private static CmSketchBlock<int, DetectIsa> blockAvx = new CmSketchBlock<int, DetectIsa>(sketchSize, EqualityComparer<int>.Default);

        private static CmSketchBlockV2<int, DisableHardwareIntrinsics> block2 = new CmSketchBlockV2<int, DisableHardwareIntrinsics>(sketchSize, EqualityComparer<int>.Default);
        private static CmSketchBlockV2<int, DetectIsa> block2Avx = new CmSketchBlockV2<int, DetectIsa>(sketchSize, EqualityComparer<int>.Default);

        [Benchmark(Baseline = true, OperationsPerInvoke = iterations)]
        public void Inc()
        {
            for (int i = 0; i < iterations; i++)
            {
                std.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncAvx()
        {
            for (int i = 0; i < iterations; i++)
            {
                avx.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncBlock()
        {
            for (int i = 0; i < iterations; i++)
            {
                block.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncBlockAvx()
        {
            for (int i = 0; i < iterations; i++)
            {
                blockAvx.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncBlock2()
        {
            for (int i = 0; i < iterations; i++)
            {
                block2.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncBlock2Avx()
        {
            for (int i = 0; i < iterations; i++)
            {
                block2Avx.Increment(i);
            }
        }
    }
}
