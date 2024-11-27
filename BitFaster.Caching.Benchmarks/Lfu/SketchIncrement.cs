
using System.Collections.Generic;
using Benchly;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lfu;

namespace BitFaster.Caching.Benchmarks.Lfu
{
    [SimpleJob(RuntimeMoniker.Net60)]
    [SimpleJob(RuntimeMoniker.Net80)]
    [SimpleJob(RuntimeMoniker.Net90)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    [ColumnChart(Title = "Sketch Increment ({JOB})", Colors = "#cd5c5c,#fa8072,#ffa07a")]
    public class SketchIncrement
    {
        const int iterations = 1_048_576;

        private CmSketchFlat<int, DisableHardwareIntrinsics> flatStd;
        private CmSketchFlat<int, DetectIsa> flatAvx;

        private CmSketchCore<int, DisableHardwareIntrinsics> blockStd;
        private CmSketchNoPin<int, DetectIsa> blockAvxNoPin;
        private CmSketchCore<int, DetectIsa> blockAvx;


        [Params(32_768, 524_288, 8_388_608, 134_217_728)]
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            flatStd = new CmSketchFlat<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            flatAvx = new CmSketchFlat<int, DetectIsa>(Size, EqualityComparer<int>.Default);

            blockStd = new CmSketchCore<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            blockAvxNoPin = new CmSketchNoPin<int, DetectIsa>(Size, EqualityComparer<int>.Default);
            blockAvx = new CmSketchCore<int, DetectIsa>(Size, EqualityComparer<int>.Default);
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = iterations)]
        public void IncFlat()
        {
            for (int i = 0; i < iterations; i++)
            {
                flatStd.Increment(i);
            }
        }
#if X64
        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncFlatAvx()
        {
            for (int i = 0; i < iterations; i++)
            {
                flatAvx.Increment(i);
            }
        }
#endif
        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncBlock()
        {
            for (int i = 0; i < iterations; i++)
            {
                blockStd.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
#if Arm64
        public void IncBlockNeonNotPinned()
#else
        public void IncBlockAvxNotPinned()
#endif
        {
            for (int i = 0; i < iterations; i++)
            {
                blockAvxNoPin.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
#if Arm64
        public void IncBlockNeonPinned()
#else
        public void IncBlockAvxPinned()
#endif
        {
            for (int i = 0; i < iterations; i++)
            {
                blockAvx.Increment(i);
            }
        }
    }
}
