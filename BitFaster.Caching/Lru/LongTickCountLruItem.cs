using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public class LongTickCountLruItem<K, V> : LruItem<K, V>
    {
        public LongTickCountLruItem(K key, V value, long tickCount)
            : base(key, value)
        {
            this.TickCount = tickCount;
        }

        public long TickCount { get; set; }
    }
}
