using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
    public  class ConcurrentLruWithExpiry<K, V, P> : ConcurrentLruTemplate<K, V, TimeStampedLruItem<K, V>, P, NoHitCounter>
        where P : struct, IPolicy<K, V, TimeStampedLruItem<K, V>>
    {
        public ConcurrentLruWithExpiry(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer, P policy)
            : base(concurrencyLevel, capacity, comparer, policy, new NoHitCounter())
        {
        }
    }
}
