
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
        public int FrequencyFlat()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += flatStd.EstimateFrequency(i) > flatStd.EstimateFrequency(i + 1) ? 1: 0;

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public int FrequencyFlatAvx()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += flatAvx.EstimateFrequency(i) > flatAvx.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public int FrequencyBlock()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += blockStd.EstimateFrequency(i) > blockStd.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public int FrequencyBlockAvx()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += blockAvx.EstimateFrequency(i) > blockAvx.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }
    }
}
