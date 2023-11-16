using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a fixed length of time.
    /// </summary>
    /// <remarks>
    /// This struct is used to abstract away the use of different time sources with different precision. 
    /// This enables use of native time values (which may be ticks or millisecs), only converting 
    /// to TimeSpan for non perf critical user code. Using long without a mul/div makes cache lookups 
    /// about 30% faster on .NET6.
    /// </remarks>
    [DebuggerDisplay("{ToTimeSpan()}")]
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

        /// <summary>
        /// Converts the duration to a TimeSpan.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan ToTimeSpan()
        {
#if NETCOREAPP3_0_OR_GREATER
            return TimeSpan.FromMilliseconds(raw);
#else
            return StopwatchTickConverter.FromTicks(raw);
#endif    
        }

        /// <summary>
        /// Returns a Duration that represents a specified TimeSpan.
        /// </summary>
        /// <param name="timeSpan">The TimeSpan to convert.</param>
        /// <returns>A duration.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Duration FromTimeSpan(TimeSpan timeSpan)
        {
#if NETCOREAPP3_0_OR_GREATER
            return new Duration((long)timeSpan.TotalMilliseconds);
#else
            return new Duration(StopwatchTickConverter.ToTicks(timeSpan));
#endif       
        }

        /// <summary>
        /// Returns a Duration that represents a specified number of milliseconds.
        /// </summary>
        /// <param name="value">A number of milliseconds</param>
        /// <returns></returns>
        public static Duration FromMilliseconds(double value)
        {
            return FromTimeSpan(TimeSpan.FromMilliseconds(value));
        }

        /// <summary>
        /// Returns a Duration that represents a specified number of seconds.
        /// </summary>
        /// <param name="value">A number of seconds</param>
        /// <returns></returns>
        public static Duration FromSeconds(double value)
        {
            return FromTimeSpan(TimeSpan.FromSeconds(value));
        }

        /// <summary>
        /// Returns a Duration that represents a specified number of milliseconds.
        /// </summary>
        /// <param name="value">A number of milliseconds</param>
        /// <returns></returns>
        public static Duration FromMinutes(double value)
        {
            return FromTimeSpan(TimeSpan.FromMinutes(value));
        }

        /// <summary>
        /// Returns a long that represents the specified Duration.
        /// </summary>
        /// <param name="d">The duration, represented as a long</param>
        public static implicit operator long(Duration d) => d.raw;

        /// <summary>
        /// Returns a Duration that represents the specified long value.
        /// </summary>
        /// <param name="b"></param>
        public static implicit operator Duration(long b) => new Duration(b);

        /// <summary>
        /// Adds two specified Duration instances.
        /// </summary>
        /// <param name="a">The first duration to add.</param>
        /// <param name="b">The second duration to add.</param>
        /// <returns>An duration whose value is the sum of the values of a and b.</returns>
        public static Duration operator +(Duration a, Duration b) => new Duration(a.raw + b.raw);

        /// <summary>
        /// Subtracts a specified Duration from another specified Duration.
        /// </summary>
        /// <param name="a">The minuend.</param>
        /// <param name="b">The subtrahend.</param>
        /// <returns>An duration whose value is the result of the value of a minus the value of b.</returns>
        public static Duration operator -(Duration a, Duration b) => new Duration(a.raw - b.raw);
    }
}
