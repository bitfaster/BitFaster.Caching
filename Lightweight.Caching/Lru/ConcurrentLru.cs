using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
    public sealed class ConcurrentLru<K, V> : ConcurrentLruTemplate<K, V, LruItem<K, V>, InfiniteTtl<K, V>, NoHitCounter>
    {
        public ConcurrentLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer)
            : base(concurrencyLevel, capacity, comparer, new InfiniteTtl<K, V>(), new NoHitCounter())
        {
        }
    }
}
