using System;
using System.Collections.Generic;
using System.Text;

namespace Lightweight.Caching.Lru
{
    public sealed class FastConcurrentTLru<K, V> : TemplateConcurrentLru<K, V, TimeStampedLruItem<K, V>, TLruPolicy<K, V>, NullHitCounter>
    {
        public FastConcurrentTLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer, TimeSpan timeToLive)
            : base(concurrencyLevel, capacity, comparer, new TLruPolicy<K, V>(timeToLive), new NullHitCounter())
        {
        }
    }
}
