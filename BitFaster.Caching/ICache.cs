using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    // can have this as internal, and inherit ICache<K, V> from ICacheBase<K, V, V> and IScopedCache : ICacheBase<K, V, Scoped<V>>

    /// <summary>
    /// Represents a generic cache of key/value pairs.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    public interface ICache<K, V, W>
    {
        /// <summary>
        /// Attempts to get the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the object from the cache that has the specified key, or the default value of the type if the operation failed.</param>
        /// <returns>true if the key was found in the cache; otherwise, false.</returns>
        bool TryGet(K key, out W value);

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to generate a value for the key.</param>
        /// <returns>The value for the key. This will be either the existing value for the key if the key is already 
        /// in the cache, or the new value if the key was not in the dictionary.</returns>
        W GetOrAdd(K key, Func<K, V> valueFactory);

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a value for the key.</param>
        /// <returns>A task that represents the asynchronous GetOrAdd operation.</returns>
        Task<W> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory);

        /// <summary>
        /// Attempts to remove the value that has the specified key.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        bool TryRemove(K key);

        /// <summary>
        /// Attempts to update the value that has the specified key.
        /// </summary>
        /// <param name="key">The key of the element to update.</param>
        /// <param name="value">The new value.</param>
        /// <returns>true if the object was updated successfully; otherwise, false.</returns>
        bool TryUpdate(K key, V value);

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist, or updates a key/value pair if the 
        /// key already exists.
        /// </summary>
        /// <param name="key">The key of the element to update.</param>
        /// <param name="value">The new value.</param>
        void AddOrUpdate(K key, V value);

        /// <summary>
        /// Removes all keys and values from the cache.
        /// </summary>
        void Clear();
    }
}
