using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru.Builder
{
    public class LruInfo<K>
    {
        public int capacity = 128;
        public int concurrencyLevel = Defaults.ConcurrencyLevel;
        public TimeSpan? expiration = null;
        public bool withMetrics = false;
        public IEqualityComparer<K> comparer = EqualityComparer<K>.Default;
    }
}
