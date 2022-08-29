using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Pad;

namespace BitFaster.Caching.Lru
{
    public struct TelemetryPolicy<K, V> : ITelemetryPolicy<K, V>
    {
        private StripedLongAdder hitCount;
        private StripedLongAdder missCount;
        private long evictedCount;
        private long updatedCount;
        private object eventSource;

        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        public double HitRatio => Total == 0 ? 0 : (double)Hits / (double)Total;

        public long Total => this.hitCount.GetValue() + this.missCount.GetValue();

        public long Hits => this.hitCount.GetValue();

        public long Misses => this.missCount.GetValue();

        public long Evicted => this.evictedCount;

        public long Updated => this.updatedCount;

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
                Interlocked.Increment(ref this.evictedCount);
            }

            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.ItemRemoved?.Invoke(this.eventSource, new ItemRemovedEventArgs<K, V>(key, value, reason));
        }

        public void OnItemUpdated(K key, V value)
        {
            Interlocked.Increment(ref this.updatedCount);
        }

        public void SetEventSource(object source)
        {
            this.hitCount = new StripedLongAdder();
            this.missCount = new StripedLongAdder();
            this.eventSource = source;
        }
    }
}
