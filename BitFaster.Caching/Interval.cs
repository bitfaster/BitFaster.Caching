using System;
using System.Diagnostics;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching
{
    public readonly struct Interval
    {
        internal readonly long raw;

        internal Interval(long raw)
        { 
            this.raw = raw; 
        }

        public static Interval GetTimestamp()
        {
#if NETCOREAPP3_0_OR_GREATER
            return new Interval(Environment.TickCount64);
#else
            return new Interval(Stopwatch.GetTimestamp());
#endif
        }

        public TimeSpan ToTimeSpan()
        {
#if NETCOREAPP3_0_OR_GREATER
            return TimeSpan.FromMilliseconds(raw);
#else
            return StopwatchTickConverter.FromTicks(raw);
#endif    
        }

        public static Interval FromTimeSpan(TimeSpan timeSpan)
        {
#if NETCOREAPP3_0_OR_GREATER
            return new Interval((long)timeSpan.TotalMilliseconds);
#else
            return new Interval(StopwatchTickConverter.ToTicks(timeSpan));
#endif       
        }
    }
}
