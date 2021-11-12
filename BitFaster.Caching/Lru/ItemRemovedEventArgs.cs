using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public class ItemRemovedEventArgs<K, V> : EventArgs
    {
        public ItemRemovedEventArgs(K key, V value, ItemRemovedReason reason)
        {
            this.Key = key;
            this.Value = value;
            this.Reason = reason;
        }

        public K Key { get; }

        public V Value { get; }

        public ItemRemovedReason Reason { get; }
    }
}
