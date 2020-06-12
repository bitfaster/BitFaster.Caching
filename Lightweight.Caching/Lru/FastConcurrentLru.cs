using System;
using System.Collections.Generic;
using System.Text;

namespace Lightweight.Caching.Lru
{
    public sealed class FastConcurrentLru<K, V> : TemplateConcurrentLru<K, V, LruItem<K, V>, LruPolicy<K, V>, NullHitCounter>
    {
        public FastConcurrentLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer)
            : base(concurrencyLevel, capacity, comparer, new LruPolicy<K, V>(), new NullHitCounter())
        {
        }
    }
}
