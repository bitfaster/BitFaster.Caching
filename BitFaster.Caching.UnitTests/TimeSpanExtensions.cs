using System;

namespace BitFaster.Caching.UnitTests
{
    internal static class TimeSpanExtensions
    {
        // .NET Framework has no TimeSpan operator*
        public static TimeSpan MultiplyBy(this TimeSpan multiplicand, int multiplier)
        {
            return TimeSpan.FromTicks(multiplicand.Ticks * multiplier);
        }

        public static TimeSpan MultiplyBy(this Duration multiplicand, int multiplier)
        {
            return TimeSpan.FromTicks(multiplicand.ToTimeSpan().Ticks * multiplier);
        }
    }
}
