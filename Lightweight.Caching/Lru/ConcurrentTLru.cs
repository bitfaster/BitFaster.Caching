using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
    public class ConcurrentTLru<K, V> : ConcurrentLruTemplate<K, V, TimeStampedLruItem<K, V>, TlruPolicy<K, V>, HitCounter>
    {
        public ConcurrentTLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer, TimeSpan timeToLive)
            : base(concurrencyLevel, capacity, comparer, new TlruPolicy<K, V>(timeToLive), new HitCounter())
        {
        }
    }
}
