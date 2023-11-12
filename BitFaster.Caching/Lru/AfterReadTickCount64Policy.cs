using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
#if NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Implement an expire after read policy.
    /// </summary>
    /// <remarks>
    /// This class measures time using Environment.TickCount64, which is significantly faster
    /// than both Stopwatch.GetTimestamp and DateTime.UtcNow. However, resolution is lower (typically 
    /// between 10-16ms), vs 1us for Stopwatch.GetTimestamp.
    /// </remarks>
    public readonly struct AfterReadLongTicksPolicy<K, V> : IItemPolicy<K, V, LongTickCountLruItem<K, V>>
    {
        private readonly long timeToLive;
        private readonly Clock clock;

        ///<inheritdoc/>
        public TimeSpan TimeToLive => TimeSpan.FromMilliseconds(timeToLive);

        /// <summary>
        /// Initializes a new instance of the AfterReadTickCount64Policy class with the specified time to live.
        /// </summary>
        /// <param name="timeToLive">The time to live.</param>
        public AfterReadLongTicksPolicy(TimeSpan timeToLive)
        {
            TimeSpan maxRepresentable = TimeSpan.FromTicks(9223372036854769664);
            if (timeToLive <= TimeSpan.Zero || timeToLive > maxRepresentable)
                Throw.ArgOutOfRange(nameof(timeToLive), $"Value must greater than zero and less than {maxRepresentable}");

            this.timeToLive = (long)timeToLive.TotalMilliseconds;
            this.clock = new Clock();
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
            item.TickCount = this.clock.LastTime;
            item.WasAccessed = true;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LongTickCountLruItem<K, V> item)
        {
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountLruItem<K, V> item)
        {
            this.clock.LastTime = Environment.TickCount64;
            if (this.clock.LastTime - item.TickCount > this.timeToLive)
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
    }
#endif
}
