
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
    [ColumnChart(Title = "Sketch Increment ({JOB})", Colors = "#cd5c5c,#fa8072,#ffa07a")]
    public class SketchIncrement
    {
        const int iterations = 1_048_576;

        private CmSketchFlat<int, DisableHardwareIntrinsics> flatStd;
        private CmSketchFlat<int, DetectIsa> flatAvx;

        private CmSketchCore<int, DisableHardwareIntrinsics> blockStd;
        private CmSketchNoPin<int, DetectIsa> blockAvxNoPin;
        private CmSketchPinNoOpt<int, DetectIsa> blockAvxPinNoOpt;
        private CmSketchCore<int, DetectIsa> blockAvx;

        [Params(512, 1024, 32_768, 524_288, 8_388_608, 134_217_728)]
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            flatStd = new CmSketchFlat<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            flatAvx = new CmSketchFlat<int, DetectIsa>(Size, EqualityComparer<int>.Default);

            blockStd = new CmSketchCore<int, DisableHardwareIntrinsics>(Size, EqualityComparer<int>.Default);
            blockAvxNoPin = new CmSketchNoPin<int, DetectIsa>(Size, EqualityComparer<int>.Default);
            blockAvxPinNoOpt = new CmSketchPinNoOpt<int, DetectIsa>(Size, EqualityComparer<int>.Default);
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

        //[Benchmark(OperationsPerInvoke = iterations)]
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
        public void IncBlockAvxNotPinned()
        {
            for (int i = 0; i < iterations; i++)
            {
                blockAvxNoPin.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncBlockAvxPinNotOpt()
        {
            for (int i = 0; i < iterations; i++)
            {
                blockAvxPinNoOpt.Increment(i);
            }
        }

        [Benchmark(OperationsPerInvoke = iterations)]
        public void IncBlockAvxPinned()
        {
            for (int i = 0; i < iterations; i++)
            {
                blockAvx.Increment(i);
            }
        }
    }
}
