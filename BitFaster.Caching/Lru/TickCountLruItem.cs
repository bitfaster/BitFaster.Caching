using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public class TickCountLruItem<K, V> : LruItem<K, V>
    {
        public TickCountLruItem(K key, V value)
            : base(key, value)
        {
            this.TickCount = Environment.TickCount;
        }

        public int TickCount { get; set; }
    }
}
