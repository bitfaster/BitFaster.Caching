using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public struct TelemetryPolicy<K, V> : ITelemetryPolicy<K, V>
    {
        private long hitCount;
        private long missCount;

        public double HitRatio => Total == 0 ? 0 : (double)hitCount / (double)Total;

        public long Total => this.hitCount + this.missCount;

        public EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        private object eventSource;

        public void IncrementMiss()
        {
            Interlocked.Increment(ref this.missCount);
        }

        public void IncrementHit()
        {
            Interlocked.Increment(ref this.hitCount);
        }

        public void OnItemRemoved(K key, V value, ItemRemovedReason reason)
        {
            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.ItemRemoved?.Invoke(this.eventSource, new ItemRemovedEventArgs<K, V>(key, value, reason));
        }

        public void SetEventSource(object source)
        {
            this.eventSource = source;
        }
    }
}
