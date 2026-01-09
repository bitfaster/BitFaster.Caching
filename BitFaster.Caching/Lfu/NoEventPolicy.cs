using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// Represents an event policy that does not have events (is disabled).
    /// This enables use of the cache without events where maximum performance is required.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    public struct NoEventPolicy<K, V> : IEventPolicy<K, V>
        where K : notnull
    {
        ///<inheritdoc/>
        public long Updated => 0;

        ///<inheritdoc/>
        public long Evicted => 0;

        ///<inheritdoc/>
        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved
        {
            // no-op, nothing is registered
            add { }
            remove { }
        }

        ///<inheritdoc/>
        public event EventHandler<ItemUpdatedEventArgs<K, V>> ItemUpdated
        {
            // no-op, nothing is registered
            add { }
            remove { }
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnItemRemoved(K key, V value, ItemRemovedReason reason)
        {
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnItemUpdated(K key, V oldValue, V value)
        {
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetEventSource(object source)
        {
        }
    }
}
