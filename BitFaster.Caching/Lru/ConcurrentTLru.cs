using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public sealed class ConcurrentTLru<K, V> : TemplateConcurrentLru<K, V, LongTickCountLruItem<K, V>, TLruLongTicksPolicy<K, V>, HitCounter>
    {
        public ConcurrentTLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer, TimeSpan timeToLive)
            : base(concurrencyLevel, capacity, comparer, new TLruLongTicksPolicy<K, V>(timeToLive), new HitCounter())
        {
        }

        public double HitRatio => this.hitCounter.HitRatio;
    }
}
