using System;
using System.Diagnostics;
using BitFaster.Caching.Counters;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents a telemetry policy with counters and events.
    /// </summary>
    /// <typeparam name="K">The type of the Key</typeparam>
    /// <typeparam name="V">The type of the value</typeparam>
    [DebuggerDisplay("Hit = {Hits}, Miss = {Misses}, Upd = {Updated}, Evict = {Evicted}")]
    public struct TelemetryPolicy<K, V> : ITelemetryPolicy<K, V>
        where K : notnull
    {
        private Counter hitCount;
        private Counter missCount;
        private Counter evictedCount;
        private Counter updatedCount;
        private object eventSource;

        ///<inheritdoc/>
        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        ///<inheritdoc/>
        public event EventHandler<ItemUpdatedEventArgs<K, V>> ItemUpdated;

        ///<inheritdoc/>
        public double HitRatio => Total == 0 ? 0 : (double)Hits / (double)Total;

        ///<inheritdoc/>
        public long Total => this.hitCount.Count() + this.missCount.Count();

        ///<inheritdoc/>
        public long Hits => this.hitCount.Count();

        ///<inheritdoc/>
        public long Misses => this.missCount.Count();

        ///<inheritdoc/>
        public long Evicted => this.evictedCount.Count();

        ///<inheritdoc/>
        public long Updated => this.updatedCount.Count();

        ///<inheritdoc/>
        public void IncrementMiss()
        {
            this.missCount.Increment();
        }

        ///<inheritdoc/>
        public void IncrementHit()
        {
            this.hitCount.Increment();
        }

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
            this.hitCount = new Counter();
            this.missCount = new Counter();
            this.evictedCount = new Counter();
            this.updatedCount = new Counter();
            this.eventSource = source;
        }
    }
}
