using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace BitFaster.Caching.Benchmarks.Lfu
{
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser(displayGenColumns: false)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class Array
    {
        private object[] array1;
        private object[] array2;

        private Sealed[] sarray1;
        private Sealed[] sarray2;

        private Wrapper<object>[] warray1;
        private Wrapper<object>[] warray2;

        [Params(4, 128, 8192, 1_048_576)]
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            array1 = new object[Size];
            array2 = new object[Size];

            sarray1 = new Sealed[Size];
            sarray2 = new Sealed[Size];

            warray1 = new Wrapper<object>[Size];
            warray2 = new Wrapper<object>[Size];

            for (int i = 0; i < Size; i++)
            {
                if (i % 3 == 0)
                {
                    array1[i] = new object();
                    sarray1[i] = new Sealed();
                    warray1[i] = new Wrapper<object>() { s = new object() };
                }
            }
        }

        [Benchmark(Baseline = true)]
        public void CopyAcross()
        {
            for (int i = 0; i < array1.Length; i++)
            {
                array2[i] = array1[i];
            }

            for (int i = 0; i < array2.Length; i++)
            {
                array1[i] = array2[i];
            }
        }

        [Benchmark]
        public void CopyAcrossSealed()
        {
            for (int i = 0; i < sarray1.Length; i++)
            {
                sarray2[i] = sarray1[i];
            }

            for (int i = 0; i < sarray2.Length; i++)
            {
                sarray1[i] = sarray2[i];
            }
        }

        [Benchmark]
        public void CopyAcrossWrapper()
        {
            for (int i = 0; i < warray1.Length; i++)
            {
                warray2[i] = warray1[i];
            }

            for (int i = 0; i < warray2.Length; i++)
            {
                warray1[i] = warray2[i];
            }
        }

        private sealed class Sealed : object { }

        private struct Wrapper<T>
        { 
            public T s;
        }
    }
}
