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
    public readonly struct ScopedLruPolicy<K, V> : IItemPolicy<K, V, Scoped<V>, LruItem<K, Scoped<V>>> where V : IDisposable
    {
        // This handles creating the scope, so caller can't mess it up
        public LruItem<K, Scoped<V>> CreateItem(K key, V value)
        {
            return new LruItem<K, Scoped<V>>(key, new Scoped<V>(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LruItem<K, Scoped<V>> item)
        {
            item.WasAccessed = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LruItem<K, Scoped<V>> item)
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteHot(LruItem<K, Scoped<V>> item)
        {
            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteWarm(LruItem<K, Scoped<V>> item)
        {
            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Cold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteCold(LruItem<K, Scoped<V>> item)
        {
            if (item.WasAccessed)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Remove;
        }


    }
}
