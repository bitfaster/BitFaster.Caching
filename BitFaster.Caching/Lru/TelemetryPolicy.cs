using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public struct TelemetryPolicy<K, V> : ITelemetryPolicy<K, V>
    {
        public long hitCount;
        public long missCount;
        public long evictedCount;
        public object eventSource;

        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        public double HitRatio => Total == 0 ? 0 : (double)Hits / (double)Total;

        public long Total => this.hitCount + this.missCount;

        public long Hits => this.hitCount;

        public long Misses => this.missCount;

        public long Evicted => this.evictedCount;

        public bool IsEnabled => true;

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
            if (reason == ItemRemovedReason.Evicted)
            {
                Interlocked.Increment(ref this.evictedCount);
            }

            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.ItemRemoved?.Invoke(this.eventSource, new ItemRemovedEventArgs<K, V>(key, value, reason));
        }

        public void SetEventSource(object source)
        {
            this.eventSource = source;
        }
    }
}
