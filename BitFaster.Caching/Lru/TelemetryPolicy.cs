using System;
using System.Diagnostics;
using BitFaster.Caching.Concurrent;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents a telemetry policy with counters and events.
    /// </summary>
    /// <typeparam name="K">The type of the Key</typeparam>
    /// <typeparam name="V">The type of the value</typeparam>
    [DebuggerDisplay("Hit = {Hits}, Miss = {Misses}, Upd = {Updated}, Evict = {Evicted}")]
    public struct TelemetryPolicy<K, V> : ITelemetryPolicy<K, V>
    {
        private LongAdder hitCount;
        private LongAdder missCount;
        private LongAdder evictedCount;
        private LongAdder updatedCount;
        private object eventSource;

        ///<inheritdoc/>
        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        ///<inheritdoc/>
        public double HitRatio => Total == 0 ? 0 : (double)Hits / (double)Total;

        ///<inheritdoc/>
        public long Total => this.hitCount.Sum() + this.missCount.Sum();

        ///<inheritdoc/>
        public long Hits => this.hitCount.Sum();

        ///<inheritdoc/>
        public long Misses => this.missCount.Sum();

        ///<inheritdoc/>
        public long Evicted => this.evictedCount.Sum();

        ///<inheritdoc/>
        public long Updated => this.updatedCount.Sum();

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
        public void OnItemUpdated(K key, V value)
        {
            this.updatedCount.Increment();
        }

        ///<inheritdoc/>
        public void SetEventSource(object source)
        {
            this.hitCount = new LongAdder();
            this.missCount = new LongAdder();
            this.evictedCount = new LongAdder();
            this.updatedCount = new LongAdder();
            this.eventSource = source;
        }
    }
}
