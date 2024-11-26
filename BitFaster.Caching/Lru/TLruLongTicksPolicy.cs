using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Time aware Least Recently Used (TLRU) is a variant of LRU which discards the least 
    /// recently used items first, and any item that has expired.
    /// </summary>
    public readonly struct TLruLongTicksPolicy<K, V> : IItemPolicy<K, V, LongTickCountLruItem<K, V>>
        where K : notnull
    {
        private readonly Duration timeToLive;

        ///<inheritdoc/>
        public TimeSpan TimeToLive => timeToLive.ToTimeSpan();

        /// <summary>
        /// Initializes a new instance of the TLruTicksPolicy class with the specified time to live.
        /// </summary>
        /// <param name="timeToLive">The time to live.</param>
        public TLruLongTicksPolicy(TimeSpan timeToLive)
        {
            if (timeToLive <= TimeSpan.Zero || timeToLive > Duration.MaxRepresentable)
                Throw.ArgOutOfRange(nameof(timeToLive), $"Value must greater than zero and less than {Duration.MaxRepresentable}");

            this.timeToLive = Duration.FromTimeSpan(timeToLive);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongTickCountLruItem<K, V> CreateItem(K key, V value)
        {
            return new LongTickCountLruItem<K, V>(key, value, Duration.SinceEpoch().raw);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LongTickCountLruItem<K, V> item)
        {
            item.MarkAccessed();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountLruItem<K, V> item)
        {
            item.TickCount = Duration.SinceEpoch().raw;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountLruItem<K, V> item)
        {
            if (Duration.SinceEpoch().raw - item.TickCount > this.timeToLive.raw)
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
            if (timespan <= TimeSpan.Zero || timespan > Duration.MaxRepresentable)
                Throw.ArgOutOfRange(nameof(timeToLive), $"Value must greater than zero and less than {Duration.MaxRepresentable}");

            return Duration.FromTimeSpan(timespan).raw;
        }

        /// <summary>
        /// Convert from ticks to a TimeSpan.
        /// </summary>
        /// <param name="ticks">The time represented as ticks.</param>
        /// <returns>The time represented as a TimeSpan.</returns>
        // backcompat: remove method (exists only for compatibility with orignal TLruLongTicksPolicy)
        public static TimeSpan FromTicks(long ticks)
        {
            return new Duration(ticks).ToTimeSpan();
        }
    }
}
