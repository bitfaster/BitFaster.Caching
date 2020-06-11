using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
    public readonly struct AbsoluteTtl<K, V> : IPolicy<K, V, TimeStampedLruItem<K, V>>
    {
        private readonly TimeSpan timeToLive;

        public static AbsoluteTtl<K, V> FromMinutes(int minutes)
        {
            return new AbsoluteTtl<K, V>(TimeSpan.FromMinutes(minutes));
        }

        public AbsoluteTtl(TimeSpan timeToLive)
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
        public bool ShouldDiscard(TimeStampedLruItem<K, V> item)
        {
            if (DateTime.UtcNow - item.TimeStamp > this.timeToLive)
            {
                return true;
            }

            return false;
        }

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
