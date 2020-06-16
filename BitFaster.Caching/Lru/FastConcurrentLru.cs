using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lru
{
    public sealed class FastConcurrentLru<K, V> : TemplateConcurrentLru<K, V, LruItem<K, V>, LruPolicy<K, V>, NullHitCounter>
    {
        public FastConcurrentLru(int capacity)
            : base(Defaults.ConcurrencyLevel, capacity, EqualityComparer<K>.Default, new LruPolicy<K, V>(), new NullHitCounter())
        {
        }

        public FastConcurrentLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer)
            : base(concurrencyLevel, capacity, comparer, new LruPolicy<K, V>(), new NullHitCounter())
        {
        }
    }
}
