using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Discards the least recently used items first. 
    /// </summary>
    public readonly struct LruPolicy<K, V> : IItemPolicy<K, V, LruItem<K, V>>
    {
        public TimeSpan TimeToLive => NoneTimePolicy.Infinite;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LruItem<K, V> CreateItem(K key, V value)
        {
            return new LruItem<K, V>(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LruItem<K, V> item)
        {
            item.WasAccessed = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LruItem<K, V> item)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LruItem<K, V> item)
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanDiscard()
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteHot(LruItem<K, V> item)
        {
            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteWarm(LruItem<K, V> item)
        {
            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteCold(LruItem<K, V> item)
        {
            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Remove;
        }
    }
}
