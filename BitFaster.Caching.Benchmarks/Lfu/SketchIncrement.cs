
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
        const int iterations = 1_048_576;

        private CmSketchFlat<int, DisableHardwareIntrinsics> flatStd;
        private CmSketchFlat<int, DetectIsa> flatAvx;

        private CmSketch<int, DisableHardwareIntrinsics> blockStd;
        private CmSketch<int, DetectIsa> blockAvx;

        [Params(32_768, 524_288, 8_388_608, 134_217_728)]
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            flatStd = new CmSketchFlat<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            flatAvx = new CmSketchFlat<int, DetectIsa>(Size, EqualityComparer<int>.Default);

            blockStd = new CmSketch<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            blockAvx = new CmSketch<int, DetectIsa>(Size, EqualityComparer<int>.Default);
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = iterations)]
        public void IncFlat()
        {
            for (int i = 0; i < iterations; i++)
            {
                flatStd.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncFlatAvx()
        {
            for (int i = 0; i < iterations; i++)
            {
                flatAvx.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncBlock()
        {
            for (int i = 0; i < iterations; i++)
            {
                blockStd.Increment(i);
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
    }
}
