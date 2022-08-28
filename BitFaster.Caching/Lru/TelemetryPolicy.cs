using System;
using System.Threading;

namespace BitFaster.Caching.Lru
{
    public struct TelemetryPolicy<K, V> : ITelemetryPolicy<K, V>
    {
        private PaddedHitCounters counters;
        private object eventSource;

        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        public double HitRatio => Total == 0 ? 0 : (double)Hits / (double)Total;

        public long Total => this.counters.hitCount + this.counters.missCount;

        public long Hits => this.counters.hitCount;

        public long Misses => this.counters.missCount;

        public long Evicted => this.counters.evictedCount;

        public long Updated => this.counters.updatedCount;

        public void IncrementMiss()
        {
            Interlocked.Increment(ref this.counters.missCount);
        }

        public void IncrementHit()
        {
            Interlocked.Increment(ref this.counters.hitCount);
        }

        public void OnItemRemoved(K key, V value, ItemRemovedReason reason)
        {
            if (reason == ItemRemovedReason.Evicted)
            {
                Interlocked.Increment(ref this.counters.evictedCount);
            }

            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.ItemRemoved?.Invoke(this.eventSource, new ItemRemovedEventArgs<K, V>(key, value, reason));
        }

        public void OnItemUpdated(K key, V value)
        {
            Interlocked.Increment(ref this.counters.updatedCount);
        }

        public void SetEventSource(object source)
        {
            this.eventSource = source;
        }
    }
}
