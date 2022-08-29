using System;
using System.Threading;
using BitFaster.Caching.Concurrent;

namespace BitFaster.Caching.Lru
{
    public struct TelemetryPolicy<K, V> : ITelemetryPolicy<K, V>
    {
        private LongAdder hitCount;
        private LongAdder missCount;
        private LongAdder evictedCount;
        private LongAdder updatedCount;
        private object eventSource;

        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        public double HitRatio => Total == 0 ? 0 : (double)Hits / (double)Total;

        public long Total => this.hitCount.Sum() + this.missCount.Sum();

        public long Hits => this.hitCount.Sum();

        public long Misses => this.missCount.Sum();

        public long Evicted => this.evictedCount.Sum();

        public long Updated => this.updatedCount.Sum();

        public void IncrementMiss()
        {
            this.missCount.Increment();
        }

        public void IncrementHit()
        {
            this.hitCount.Increment();
        }

        public void OnItemRemoved(K key, V value, ItemRemovedReason reason)
        {
            if (reason == ItemRemovedReason.Evicted)
            {
                this.evictedCount.Increment();
            }

            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.ItemRemoved?.Invoke(this.eventSource, new ItemRemovedEventArgs<K, V>(key, value, reason));
        }

        public void OnItemUpdated(K key, V value)
        {
            this.updatedCount.Increment();
        }

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
