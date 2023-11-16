using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
// backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Time aware Least Recently Used (TLRU) is a variant of LRU which discards the least 
    /// recently used items first, and any item that has expired.
    /// </summary>
    /// <remarks>
    /// This class measures time using Environment.TickCount64, which is significantly faster
    /// than both Stopwatch.GetTimestamp and DateTime.UtcNow. However, resolution is lower (typically 
    /// between 10-16ms), vs 1us for Stopwatch.GetTimestamp.
    /// </remarks>
    // backcompat: rename to TLruTickCount64Policy
    public readonly struct TLruLongTicksPolicy<K, V> : IItemPolicy<K, V, LongTickCountLruItem<K, V>>
    {
        private readonly long timeToLive;

        ///<inheritdoc/>
        public TimeSpan TimeToLive => TimeSpan.FromMilliseconds(timeToLive);

        /// <summary>
        /// Initializes a new instance of the TLruTicksPolicy class with the specified time to live.
        /// </summary>
        /// <param name="timeToLive">The time to live.</param>
        public TLruLongTicksPolicy(TimeSpan timeToLive)
        {
            if (timeToLive <= TimeSpan.Zero || timeToLive > Time.MaxRepresentable)
                Throw.ArgOutOfRange(nameof(timeToLive), $"Value must greater than zero and less than {Time.MaxRepresentable}");

            this.timeToLive = (long)timeToLive.TotalMilliseconds;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongTickCountLruItem<K, V> CreateItem(K key, V value)
        {
            return new LongTickCountLruItem<K, V>(key, value, Environment.TickCount64);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountLruItem<K, V> item)
        {
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountLruItem<K, V> item)
        {
            item.TickCount = Environment.TickCount64;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountLruItem<K, V> item)
        {
            if (Environment.TickCount64 - item.TickCount > this.timeToLive)
            {
                return true;
            }

            return false;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanDiscard()
        {
            return true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteHot(LongTickCountLruItem<K, V> item)
        {
            if (this.ShouldDiscard(item))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteWarm(LongTickCountLruItem<K, V> item)
        {
            if (this.ShouldDiscard(item))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteCold(LongTickCountLruItem<K, V> item)
        {
            if (this.ShouldDiscard(item))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Remove;
        }

        /// <summary>
        /// Convert from TimeSpan to ticks.
        /// </summary>
        /// <param name="timespan">The time represented as a TimeSpan.</param>
        /// <returns>The time represented as ticks.</returns>
        // backcompat: remove method (exists only for compatibility with orignal TLruLongTicksPolicy)
        public static long ToTicks(TimeSpan timespan)
        {
            return StopwatchTickConverter.ToTicks(timespan);
        }

        /// <summary>
        /// Convert from ticks to a TimeSpan.
        /// </summary>
        /// <param name="ticks">The time represented as ticks.</param>
        /// <returns>The time represented as a TimeSpan.</returns>
        // backcompat: remove method (exists only for compatibility with orignal TLruLongTicksPolicy)
        public static TimeSpan FromTicks(long ticks)
        {
            return StopwatchTickConverter.FromTicks(ticks);
        }
    }
#endif
}
