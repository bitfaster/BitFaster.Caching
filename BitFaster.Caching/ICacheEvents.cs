using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents the events that fire when actions are performed on the cache.
    /// If events are disabled, no events will be registered, and none will fire.
    /// </summary>
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
