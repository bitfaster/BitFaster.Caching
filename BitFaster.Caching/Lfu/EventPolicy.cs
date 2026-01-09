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
    public struct EventPolicy<K, V> : IEventPolicy<K, V>
        where K : notnull
    {
        private Counter evictedCount;
        private Counter updatedCount;
        private object eventSource;

        ///<inheritdoc/>
        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        ///<inheritdoc/>
        public event EventHandler<ItemUpdatedEventArgs<K, V>> ItemUpdated;

        ///<inheritdoc/>
        public long Evicted => this.evictedCount.Count();

        ///<inheritdoc/>
        public long Updated => this.updatedCount.Count();

        ///<inheritdoc/>
        public void OnItemRemoved(K key, V value, ItemRemovedReason reason)
        {
            if (reason == ItemRemovedReason.Evicted)
            {
                this.evictedCount.Increment();
            }

            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.ItemRemoved?.Invoke(this.eventSource, new ItemRemovedEventArgs<K, V>(key, value, reason));
        }

        ///<inheritdoc/>
        public void OnItemUpdated(K key, V oldValue, V newValue)
        {
            this.updatedCount.Increment();

            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.ItemUpdated?.Invoke(this.eventSource, new ItemUpdatedEventArgs<K, V>(key, oldValue, newValue));
        }

        ///<inheritdoc/>
        public void SetEventSource(object source)
        {
            this.evictedCount = new Counter();
            this.updatedCount = new Counter();
            this.eventSource = source;
        }
    }
}
