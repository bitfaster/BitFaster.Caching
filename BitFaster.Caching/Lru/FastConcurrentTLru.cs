using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lru
{
    public sealed class FastConcurrentTLru<K, V> : TemplateConcurrentLru<K, V, LongTickCountLruItem<K, V>, TLruLongTicksPolicy<K, V>, NullHitCounter>
    {
        public FastConcurrentTLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer, TimeSpan timeToLive)
            : base(concurrencyLevel, capacity, comparer, new TLruLongTicksPolicy<K, V>(timeToLive), new NullHitCounter())
        {
        }
    }
}
