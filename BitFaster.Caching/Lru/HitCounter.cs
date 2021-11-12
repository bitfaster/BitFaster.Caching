using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public struct HitCounter<K, V> : IHitCounter<K, V>
    {
        private long hitCount;
        private long missCount;

        public double HitRatio => Total == 0 ? 0 : (double)hitCount / (double)Total;

        public long Total => this.hitCount + this.missCount;

        public EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

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
            this.ItemRemoved?.Invoke(this, new ItemRemovedEventArgs<K, V>(key, value, reason));
        }
    }
}
