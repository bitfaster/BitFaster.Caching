using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BitFaster.Caching.Lfu;
using MathNet.Numerics.LinearAlgebra.Factorization;

namespace BitFaster.Caching.Benchmarks.Lfu
{
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class SketchIncrement
    {
        private static CmSketch<int, DisableAvx2> std = new CmSketch<int, DisableAvx2>(10, EqualityComparer<int>.Default);
        private static CmSketch<int, DetectAvx2> avx = new CmSketch<int, DetectAvx2>(10, EqualityComparer<int>.Default);

        [Benchmark(Baseline = true)]
        public void Inc()
        {
            for (int i = 0; i < 128; i++)
            {
                std.Increment(i);
            }
        }

        [Benchmark()]
        public void IncAvx()
        {
            for (int i = 0; i < 128; i++)
            {
                avx.Increment(i);
            }
        }
    }
}
