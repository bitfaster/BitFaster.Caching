using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lru
{
    public sealed class FastConcurrentTLru<K, V> : TemplateConcurrentLru<K, V, TickCountLruItem<K, V>, TLruPolicyTicks<K, V>, NullHitCounter>
    {
        public FastConcurrentTLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer, TimeSpan timeToLive)
            : base(concurrencyLevel, capacity, comparer, new TLruPolicyTicks<K, V>(timeToLive), new NullHitCounter())
        {
        }
    }
}
