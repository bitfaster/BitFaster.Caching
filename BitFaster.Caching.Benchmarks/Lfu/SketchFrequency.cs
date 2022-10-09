
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.ThroughputAnalysis;

namespace BitFaster.Caching.Benchmarks.Lfu
{
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class SketchFrequency
    {
        private CmSketchFlat<int, DisableHardwareIntrinsics> flatStd;
        private CmSketchFlat<int, DetectIsa> flatAvx;

        private CmSketch<int, DisableHardwareIntrinsics> blockStd;
        private CmSketch<int, DetectIsa> blockAvx;

        private static int[] ints;
        private static int mask = 0;
        private int index = 0;

        [Params(32_768, 524_288, 8_388_608, 134_217_728)]
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            if (ints == null)
            {
                int size = 2 << 14;
                mask = size - 1;
                ints = FastZipf.Generate(size, 0.99, size / 3);
            }

            flatStd = new CmSketchFlat<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            flatAvx = new CmSketchFlat<int, DetectIsa>(Size, EqualityComparer<int>.Default);

            blockStd = new CmSketch<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            blockAvx = new CmSketch<int, DetectIsa>(Size, EqualityComparer<int>.Default);

            for (int i = 0; i < ints.Length; i++)
            {
                flatStd.Increment(i);
                flatAvx.Increment(i);
                blockStd.Increment(i);
                blockAvx.Increment(i);
            }
        }

        [Benchmark(Baseline = true)]
        public int FrequencyFlat()
        {
            return flatStd.EstimateFrequency(ints[index++ & mask]);
        }

        [Benchmark()]
        public int FrequencyFlatAvx()
        {
            return flatAvx.EstimateFrequency(ints[index++ & mask]);
        }

        [Benchmark()]
        public int FrequencyBlock()
        {
            return blockStd.EstimateFrequency(ints[index++ & mask]);
        }

        [Benchmark()]
        public int FrequencyBlockAvx()
        {
            return blockAvx.EstimateFrequency(ints[index++ & mask]);
        }
    }
}
