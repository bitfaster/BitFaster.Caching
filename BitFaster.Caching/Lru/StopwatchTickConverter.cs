using System;
using System.Diagnostics;

namespace BitFaster.Caching.Lru
{
    internal static class StopwatchTickConverter
    {
        // On some platforms (e.g. MacOS), stopwatch and timespan have different resolution
        private static readonly double stopwatchAdjustmentFactor = Stopwatch.Frequency / (double)TimeSpan.TicksPerSecond;

        internal static long ToTicks(TimeSpan timespan)
        {
            // mac adjustment factor is 100, giving lowest maximum TTL on mac platform - use same upper limit on all platforms for consistency
            // this also avoids overflow when multipling long.MaxValue by 1.0
            double maxTicks = long.MaxValue * 0.01d;

            if (timespan <= TimeSpan.Zero || timespan.Ticks >= maxTicks)
            {
                TimeSpan maxRepresentable = TimeSpan.FromTicks((long)maxTicks);
                Ex.ThrowArgOutOfRange(nameof(timespan), $"Value must be greater than zero and less than {maxRepresentable}");
            }

            return (long)(timespan.Ticks * stopwatchAdjustmentFactor);
        }

        internal static TimeSpan FromTicks(long ticks)
        {
            return TimeSpan.FromTicks((long)(ticks / stopwatchAdjustmentFactor));
        }
    }
}
