using System;

namespace BitFaster.Caching
{
    /// <summary>
    /// Provides data for the ItemUpdated event.
    /// </summary>
    /// <typeparam name="K">The type of the updated item key.</typeparam>
    /// <typeparam name="V">The type of the updated item value.</typeparam>
    public class ItemUpdatedEventArgs<K, V> : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the ItemUpdatedEventArgs class using the specified key, old value and new value.
        /// </summary>
        /// <param name="key">The key of the item that was updated.</param>
        /// <param name="oldValue">The old cache value.</param>
        /// <param name="newValue">The new cache value.</param>
        public ItemUpdatedEventArgs(K key, V oldValue, V newValue)
        {
            this.Key = key;
            this.OldValue = oldValue;
            this.NewValue = newValue;
        }

        /// <summary>
        /// Gets the key of the item that was updated.
        /// </summary>
        public K Key { get; }

        /// <summary>
        /// Gets the old value of the item.
        /// </summary>
        public V OldValue { get; }

        /// <summary>
        /// Gets the new value of the item.
        /// </summary>
        public V NewValue { get; }
    }
}
