using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace BitFaster.Caching.Benchmarks
{
#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    [SimpleJob(RuntimeMoniker.Net48)]
#endif
    [SimpleJob(RuntimeMoniker.Net90)]
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    public class TimeBenchmarks
    {
        private static readonly Stopwatch sw = Stopwatch.StartNew();

        // .NET 8 onwards has TimeProvider.System
        // https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider.system?view=net-8.0
        // This is based on either Stopwatch (high perf timestamp) or UtcNow (time zone based on local)

        [Benchmark(Baseline = true)]
        public DateTime DateTimeUtcNow()
        {
            return DateTime.UtcNow;
        }

        [Benchmark()]
        public DateTimeOffset DateTimeOffsetUtcNow()
        {
            return DateTimeOffset.UtcNow;
        }

        [Benchmark()]
        public int EnvironmentTickCount()
        {
            return Environment.TickCount;
        }

        [Benchmark()]
        public long EnvironmentTickCount64()
        {
#if NETCOREAPP3_0_OR_GREATER
            return Environment.TickCount64;
#else
            return 0;
#endif
        }

        [Benchmark()]
        public long PInvokeTickCount64()
        {
            return TickCount64.Current;
        }

        [Benchmark()]
        public long StopWatchGetElapsed()
        {
            return sw.ElapsedTicks;
        }

        [Benchmark()]
        public long StopWatchGetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        [Benchmark()]
        public Duration DurationSinceEpoch()
        {
            return Duration.SinceEpoch();
        }

        [Benchmark()]
        public long SystemTimeProvider()
        {
#if NET8_0_OR_GREATER
            return TimeProvider.System.GetTimestamp();
#else
            return 0;
#endif
        }
    }

    public static class TickCount64
    {
        public static long Current => GetTickCount64();

        [DllImport("kernel32")]
        private static extern long GetTickCount64();
    }
}
