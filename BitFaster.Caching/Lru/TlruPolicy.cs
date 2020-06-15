using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Time aware Least Recently Used (TLRU) is a variant of LRU which discards the least 
    /// recently used items first, and any item that has expired.
    /// </summary>
    public readonly struct TLruPolicy<K, V> : IPolicy<K, V, TimeStampedLruItem<K, V>>
    {
        private readonly TimeSpan timeToLive;

        public DateTime UtcNow() { return DateTime.UtcNow; }

        public TLruPolicy(TimeSpan timeToLive)
        {
            this.timeToLive = timeToLive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeStampedLruItem<K, V> CreateItem(K key, V value)
        {
            return new TimeStampedLruItem<K, V>(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(TimeStampedLruItem<K, V> item)
        {
            item.WasAccessed = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(TimeStampedLruItem<K, V> item, ref DateTime now)
        {
            if (DateTime.UtcNow - item.TimeStamp > this.timeToLive)
            {
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteHot(TimeStampedLruItem<K, V> item, ref DateTime now)
        {
            if (this.ShouldDiscard(item, ref now))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteWarm(TimeStampedLruItem<K, V> item, ref DateTime now)
        {
            if (this.ShouldDiscard(item, ref now))
            {
                return ItemDestination.Remove;
            }

            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteCold(TimeStampedLruItem<K, V> item, ref DateTime now)
        {
            if (this.ShouldDiscard(item, ref now))
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
