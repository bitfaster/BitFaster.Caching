﻿using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lru
{
    public sealed class FastConcurrentTLru<K, V> : TemplateConcurrentLru<K, V, TickCountLruItem<K, V>, TLruTicksPolicy<K, V>, NullHitCounter>
    {
        public FastConcurrentTLru(int capacity)
            : base(Defaults.ConcurrencyLevel, capacity, EqualityComparer<K>.Default, new TLruTicksPolicy<K, V>(), new NullHitCounter())
        {
        }

        public FastConcurrentTLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer, TimeSpan timeToLive)
            : base(concurrencyLevel, capacity, comparer, new TLruTicksPolicy<K, V>(timeToLive), new NullHitCounter())
        {
        }
    }
}
