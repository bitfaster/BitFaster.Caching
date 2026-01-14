using System;
using System.Diagnostics;
using BitFaster.Caching.Counters;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// Represents an event policy with events.
    /// </summary>
    /// <typeparam name="K">The type of the Key</typeparam>
    /// <typeparam name="V">The type of the value</typeparam>
    [DebuggerDisplay("Upd = {Updated}, Evict = {Evicted}")]
    internal struct EventPolicy<K, V> : IEventPolicy<K, V>
        where K : notnull
    {
        private object eventSource;

        ///<inheritdoc/>
        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        ///<inheritdoc/>
        public event EventHandler<ItemUpdatedEventArgs<K, V>> ItemUpdated;

        ///<inheritdoc/>
        public void OnItemRemoved(K key, V value, ItemRemovedReason reason)
        {
            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.ItemRemoved?.Invoke(this.eventSource, new ItemRemovedEventArgs<K, V>(key, value, reason));
        }

        ///<inheritdoc/>
        public void OnItemUpdated(K key, V oldValue, V newValue)
        {
            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.ItemUpdated?.Invoke(this.eventSource, new ItemUpdatedEventArgs<K, V>(key, oldValue, newValue));
        }

        ///<inheritdoc/>
        public void SetEventSource(object source)
        {
            this.eventSource = source;
        }
    }
}
