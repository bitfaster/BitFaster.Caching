
using System.Collections.Generic;
using Benchly;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lfu;

namespace BitFaster.Caching.Benchmarks.Lfu
{
#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 4)]
#endif
    [SimpleJob(RuntimeMoniker.Net60)]
    [SimpleJob(RuntimeMoniker.Net80)]
    [SimpleJob(RuntimeMoniker.Net90)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    [ColumnChart(Title = "Sketch Frequency ({JOB})", Colors = "#cd5c5c,#fa8072,#ffa07a")]
    public class SketchFrequency
    {
        const int sketchSize = 1_048_576;
        const int iterations = 1_048_576;

        private CmSketchFlat<int, DisableHardwareIntrinsics> flatStd;
        private CmSketchFlat<int, DetectIsa> flatAvx;

        private CmSketchNoPin<int, DisableHardwareIntrinsics> blockStdNoUnroll;
        private CmSketchCore<int, DisableHardwareIntrinsics> blockStdUnroll;
        private CmSketchNoPin<int, DetectIsa> blockAvxNoPin;
        private CmSketchCore<int, DetectIsa> blockAvx;

        [Params(32_768, 524_288, 8_388_608, 134_217_728)]
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            flatStd = new CmSketchFlat<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            flatAvx = new CmSketchFlat<int, DetectIsa>(Size, EqualityComparer<int>.Default);

            blockStdNoUnroll = new CmSketchNoPin<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            blockStdUnroll = new CmSketchCore<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            blockAvxNoPin = new CmSketchNoPin<int, DetectIsa>(Size, EqualityComparer<int>.Default);
            blockAvx = new CmSketchCore<int, DetectIsa>(Size, EqualityComparer<int>.Default);
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = iterations)]
        public int FrequencyFlat()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += flatStd.EstimateFrequency(i) > flatStd.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }
#if X64
        [Benchmark(OperationsPerInvoke = iterations)]
        public int FrequencyFlatAvx()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += flatAvx.EstimateFrequency(i) > flatAvx.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }
#endif
        [Benchmark(OperationsPerInvoke = iterations)]
        public int FrequencyBlock()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += blockStdNoUnroll.EstimateFrequency(i) > blockStdNoUnroll.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public int FrequencyBlockUnroll()
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += blockStdUnroll.EstimateFrequency(i) > blockStdUnroll.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]
#if Arm64
        public int FrequencyBlockNeonNotPinned()
#else
        public int FrequencyBlockAvxNotPinned()
#endif
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += blockAvxNoPin.EstimateFrequency(i) > blockAvx.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }

        [Benchmark(OperationsPerInvoke = iterations)]

#if Arm64
        public int FrequencyBlockNeonPinned()
#else
        public int FrequencyBlockAvxPinned()
#endif
        {
            int count = 0;
            for (int i = 0; i < iterations; i++)
                count += blockAvx.EstimateFrequency(i) > blockAvx.EstimateFrequency(i + 1) ? 1 : 0;

            return count;
        }
    }
}
