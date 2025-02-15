using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Discards the least recently used items first. 
    /// </summary>
    public readonly struct LruPolicy<K, V> : IItemPolicy<K, V, LruItem<K, V>>
        where K : notnull
    {
        ///<inheritdoc/>
        public TimeSpan TimeToLive => Timeout.InfiniteTimeSpan;

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LruItem<K, V> CreateItem(K key, V value)
        {
            return new LruItem<K, V>(key, value);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch(LruItem<K, V> item)
        {
            item.MarkAccessed();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(LruItem<K, V> item)
        {
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LruItem<K, V> item)
        {
            return false;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanDiscard()
        {
            return false;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemDestination RouteHot(LruItem<K, V> item)
        {
            if (item.WasRemoved)
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
        public ItemDestination RouteWarm(LruItem<K, V> item)
        {
            if (item.WasRemoved)
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
        public ItemDestination RouteCold(LruItem<K, V> item)
        {
            if (item.WasAccessed & !item.WasRemoved)
            {
                return ItemDestination.Warm;
            }

            return ItemDestination.Remove;
        }
    }
}
