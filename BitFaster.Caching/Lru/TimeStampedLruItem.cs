using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public class TimeStampedLruItem<K, V> : LruItem<K, V>
    {
        public TimeStampedLruItem(K key, V value)
            : base(key, value)
        {
            this.TimeStamp = DateTime.UtcNow;
        }

        public DateTime TimeStamp { get; set; }
    }
}
