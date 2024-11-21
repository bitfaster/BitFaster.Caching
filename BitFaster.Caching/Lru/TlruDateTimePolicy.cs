using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Time aware Least Recently Used (TLRU) is a variant of LRU which discards the least 
    /// recently used items first, and any item that has expired.
    /// </summary>
    public readonly struct TLruDateTimePolicy<K, V> : IItemPolicy<K, V, TimeStampedLruItem<K, V>>
        where K : notnull
    {
        private readonly TimeSpan timeToLive;

        ///<inheritdoc/>
        public TimeSpan TimeToLive => timeToLive;

        /// <summary>
        /// Initializes a new instance of the TLruDateTimePolicy class with the specified time to live.
        /// </summary>
        /// <param name="timeToLive">The time to live.</param>
        public TLruDateTimePolicy(TimeSpan timeToLive)
        {
            this.timeToLive = timeToLive;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeStampedLruItem<K, V> CreateItem(K key, V value)
        {
            return new TimeStampedLruItem<K, V>(key, value);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(TimeStampedLruItem<K, V> item)
        {
            item.MarkAccessed();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(TimeStampedLruItem<K, V> item)
        {
            item.TimeStamp = DateTime.UtcNow;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(TimeStampedLruItem<K, V> item)
        {
            if (DateTime.UtcNow - item.TimeStamp > this.timeToLive)
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
        public ItemDestination RouteHot(TimeStampedLruItem<K, V> item)
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
        public ItemDestination RouteWarm(TimeStampedLruItem<K, V> item)
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
        public ItemDestination RouteCold(TimeStampedLruItem<K, V> item)
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
