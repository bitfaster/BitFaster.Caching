using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace BitFaster.Caching.Benchmarks
{
#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [SimpleJob(RuntimeMoniker.Net48)]
    [LegacyJitX86Job]
#endif
    [SimpleJob(RuntimeMoniker.Net60)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class _32bitTImeTest
    {
        private static TimeOrig timeOrig = new TimeOrig();
        private static Time time = new Time();

        [Benchmark(Baseline =true)]
        public long TimeOriginal()
        { 
            return timeOrig.Last; 
        }

        [Benchmark()]
        public long Time2()
        { 
            return time.Last; 
        }

        [Benchmark()]
        public Duration GetActualTime()
        { 
            return Duration.SinceEpoch(); 
        }
    }

    internal class TimeOrig
    { 
        internal long Last { get; set;}
    }

    internal class Time
    {
        private static readonly bool Is64Bit = Environment.Is64BitProcess;

        private long time;

        /// <summary>
        /// Gets or sets the last time.
        /// </summary>
        internal long Last 
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            { 
                if (Is64Bit)
                {
                    return time;
                }
                else
                {
                    return Interlocked.Read(ref time);
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (Is64Bit)
                {
                    time = value;
                }
                else
                {
                    Interlocked.CompareExchange(ref time, value, time);
                }
            }
        }
    }
}
