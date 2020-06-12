using System;
using System.Collections.Generic;
using System.Text;

namespace Lightweight.Caching.Lru
{
    public class FastConcurrentTLru<K, V> : ConcurrentLruTemplate<K, V, TimeStampedLruItem<K, V>, TlruPolicy<K, V>, NullHitCounter>
    {
        public FastConcurrentTLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer, TimeSpan timeToLive)
            : base(concurrencyLevel, capacity, comparer, new TlruPolicy<K, V>(timeToLive), new NullHitCounter())
        {
        }
    }
}
