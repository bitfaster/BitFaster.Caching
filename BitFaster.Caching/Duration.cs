using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

        // MacOS Stopwatch adjustment factor is 100, giving lowest maximum TTL on mac platform - use same upper limit on all platforms for consistency
        // this also avoids overflow when multipling long.MaxValue by 1.0
        internal static readonly TimeSpan MaxRepresentable = TimeSpan.FromTicks((long)(long.MaxValue / 100.0d));

        internal static readonly Duration Zero = new Duration(0);

#if NETCOREAPP3_0_OR_GREATER
        private static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
#else
        private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        [DllImport("kernel32")]
        internal static extern long GetTickCount64();
#endif

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
            if (IsMacOS)
            {
                return new Duration(Stopwatch.GetTimestamp());
            }
            else
            {
                return new Duration(Environment.TickCount64);
            }
#else
            if (IsWindows)
            {
                return new Duration(GetTickCount64());
            }
            else
            {
                // Warning: not currently covered by unit tests
                return new Duration(Stopwatch.GetTimestamp());
            }
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
            if (IsMacOS)
            {
                return StopwatchTickConverter.FromTicks(raw);
            }
            else
            {
                return TimeSpan.FromMilliseconds(raw);
            }
#else
            if (IsWindows)
            {
                return TimeSpan.FromMilliseconds(raw);
            }
            else
            {
                // Warning: not currently covered by unit tests
                return StopwatchTickConverter.FromTicks(raw);
            }
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
            if (IsMacOS)
            {
                return new Duration(StopwatchTickConverter.ToTicks(timeSpan));
            }
            else
            {
                return new Duration((long)timeSpan.TotalMilliseconds);
            }
#else
            if (IsWindows)
            {
                return new Duration((long)timeSpan.TotalMilliseconds);
            }
            else
            {
                // Warning: not currently covered by unit tests
                return new Duration(StopwatchTickConverter.ToTicks(timeSpan));
            }
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
        /// Returns a Duration that represents a specified number of minutes.
        /// </summary>
        /// <param name="value">A number of minutes</param>
        /// <returns></returns>
        public static Duration FromMinutes(double value)
        {
            return FromTimeSpan(TimeSpan.FromMinutes(value));
        }

        /// <summary>
        /// Returns a Duration that represents a specified number of hours.
        /// </summary>
        /// <param name="value">A number of hours</param>
        /// <returns></returns>
        public static Duration FromHours(double value)
        {
            return FromTimeSpan(TimeSpan.FromHours(value));
        }

        /// <summary>
        /// Returns a Duration that represents a specified number of days.
        /// </summary>
        /// <param name="value">A number of days</param>
        /// <returns></returns>
        public static Duration FromDays(double value)
        {
            return FromTimeSpan(TimeSpan.FromDays(value));
        }

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

        /// <summary>
        /// Returns a value that indicates whether a specified Duration is greater than another specified Duration.    
        /// </summary>
        /// <param name="a">The first duration to compare.</param>
        /// <param name="b">The second duration to compare.</param>
        /// <returns>true if the value of a is greater than the value of b; otherwise, false.</returns>
        public static bool operator >(Duration a, Duration b) => a.raw > b.raw;

        /// <summary>
        /// Returns a value that indicates whether a specified Duration is less than another specified Duration.    
        /// </summary>
        /// <param name="a">The first duration to compare.</param>
        /// <param name="b">The second duration to compare.</param>
        /// <returns>true if the value of a is less than the value of b; otherwise, false.</returns>
        public static bool operator <(Duration a, Duration b) => a.raw < b.raw;
    }
}
