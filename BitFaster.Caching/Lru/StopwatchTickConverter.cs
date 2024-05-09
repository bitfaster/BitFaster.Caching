using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
    internal static class StopwatchTickConverter
    {
        // On some platforms (e.g. MacOS), stopwatch and timespan have different resolution
        internal static readonly double stopwatchAdjustmentFactor = Stopwatch.Frequency / (double)TimeSpan.TicksPerSecond;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ToTicks(TimeSpan timespan)
        {
            return (long)(timespan.Ticks * stopwatchAdjustmentFactor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TimeSpan FromTicks(long ticks)
        {
            return TimeSpan.FromTicks((long)(ticks / stopwatchAdjustmentFactor));
        }
    }
}
