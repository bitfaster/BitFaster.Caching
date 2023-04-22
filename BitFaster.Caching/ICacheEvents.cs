using System;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents the events that fire when actions are performed on the cache.
    /// </summary>
    public interface ICacheEvents<K, V>
    {
        /// <summary>
        /// Occurs when an item is removed from the cache.
        /// </summary>
        event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved;

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Occurs when an item is updated.
        /// </summary>
        event EventHandler<ItemUpdatedEventArgs<K, V>> ItemUpdated
        {
            add { }
            remove { }
        }
#endif
    }
}
