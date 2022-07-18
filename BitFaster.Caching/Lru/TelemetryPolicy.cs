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
        private Data data;

        public double HitRatio => Total == 0 ? 0 : (double)Hits / (double)Total;

        public long Total => this.data.hitCount + this.data.missCount;

        public long Hits => this.data.hitCount;

        public long Misses => this.data.missCount;

        public long Evicted => this.data.evictedCount;

        public bool IsEnabled => true;

        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved
        {
            add { this.data.ItemRemoved += value; }
            remove { this.data.ItemRemoved -= value; }
        }

        public void IncrementMiss()
        {
            Interlocked.Increment(ref this.data.missCount);
        }

        public void IncrementHit()
        {
            Interlocked.Increment(ref this.data.hitCount);
        }

        public void OnItemRemoved(K key, V value, ItemRemovedReason reason)
        {
            if (reason == ItemRemovedReason.Evicted)
            {
                Interlocked.Increment(ref this.data.evictedCount);
            }

            // passing 'this' as source boxes the struct, and is anyway the wrong object
            this.data.ItemRemoved?.Invoke(this.data.eventSource, new ItemRemovedEventArgs<K, V>(key, value, reason));
        }

        public void SetEventSource(object source)
        {
            this.data = new Data(); 

            this.data.eventSource = source;
        }

        // Data exists because TelemetryPolicy is a struct (to get magic JIT optimizations),
        // but returning it as a property from TemplateConcurrentLru causes a defensive copy
        // to be made. By storing all the data in an encapsulated reference type, the 
        // defensive copies of the value type have no effect - they point to the same ref.
        private class Data
        {
            public long hitCount;
            public long missCount;
            public long evictedCount;
            public object eventSource;

            public EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;
        }
    }
}
