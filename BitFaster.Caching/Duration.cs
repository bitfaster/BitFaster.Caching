using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a period of time between two events.
    /// </summary>
    /// <remarks>
    /// This struct is used to abstract away the use of different time sources. We select
    /// the fastest time source for each platform.
    /// </remarks>
    public readonly struct Duration

    {
        internal readonly long raw;

        internal Duration(long raw)
        { 
            this.raw = raw; 
        }

        /// <summary>
        /// Gets the time since the system epoch.
        /// </summary>
        /// <returns>A duration</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Duration SinceEpoch()
        {
#if NETCOREAPP3_0_OR_GREATER
            return new Duration(Environment.TickCount64);
#else
            return new Duration(Stopwatch.GetTimestamp());
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan ToTimeSpan()
        {
#if NETCOREAPP3_0_OR_GREATER
            return TimeSpan.FromMilliseconds(raw);
#else
            return StopwatchTickConverter.FromTicks(raw);
#endif    
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Duration FromTimeSpan(TimeSpan timeSpan)
        {
#if NETCOREAPP3_0_OR_GREATER
            return new Duration((long)timeSpan.TotalMilliseconds);
#else
            return new Duration(StopwatchTickConverter.ToTicks(timeSpan));
#endif       
        }

        public static Duration FromMinutes(double value)
        {
            return FromTimeSpan(TimeSpan.FromMinutes(value));
        }

        public static Duration FromSeconds(double value)
        {
            return FromTimeSpan(TimeSpan.FromSeconds(value));
        }

        public static implicit operator long(Duration d) => d.raw;

        public static implicit operator Duration(long b) => new Duration(b);

        public static Duration operator +(Duration a, Duration b) => new Duration(a.raw + b.raw);

        public static Duration operator -(Duration a, Duration b) => new Duration(a.raw + b.raw);
    }
}
