using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Implement an expire after access policy.
    /// </summary>
    internal readonly struct AfterAccessPolicy<K, V> : IItemPolicy<K, V, LongTickCountLruItem<K, V>>
    {
        private readonly Duration timeToLive;
        private readonly Time time;

        ///<inheritdoc/>
        public TimeSpan TimeToLive => timeToLive.ToTimeSpan();

        /// <summary>
        /// Initializes a new instance of the AfterReadTickCount64Policy class with the specified time to live.
        /// </summary>
        /// <param name="timeToLive">The time to live.</param>
        public AfterAccessPolicy(TimeSpan timeToLive)
        {
            if (timeToLive <= TimeSpan.Zero || timeToLive > Time.MaxRepresentable)
                Throw.ArgOutOfRange(nameof(timeToLive), $"Value must greater than zero and less than {Time.MaxRepresentable}");

            this.timeToLive = Duration.FromTimeSpan(timeToLive);
            this.time = new Time();
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
            item.TickCount = this.time.Last;
            item.WasAccessed = true;
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
            this.time.Last = Duration.SinceEpoch().raw;
            if (this.time.Last - item.TickCount > this.timeToLive.raw)
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
}
