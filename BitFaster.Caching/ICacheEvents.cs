using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching
{
    public interface ICacheEvents<K, V>
    {
        /// <summary>
        /// Occurs when an item is removed from the cache.
        /// </summary>
        event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

        /// <summary>
        /// Gets a value indicating whether events are enabled.
        /// </summary>
        bool IsEnabled { get; }
    }
}
