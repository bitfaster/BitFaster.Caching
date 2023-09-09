using System;
using System.Collections.Generic;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a generic cache of key/value pairs.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    public interface ICache<K, V> : IEnumerable<KeyValuePair<K, V>>
    {
        /// <summary>
        /// Gets the number of items currently held in the cache.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the cache metrics, if configured.
        /// </summary>
        Optional<ICacheMetrics> Metrics { get; }

        /// <summary>
        /// Gets the cache events, if configured.
        /// </summary>
        Optional<ICacheEvents<K, V>> Events { get; }

        /// <summary>
        /// Gets the cache policy.
        /// </summary>
        CachePolicy Policy { get; }

        /// <summary>
        /// Gets a collection containing the keys in the cache.
        /// </summary>
        ICollection<K> Keys { get; }

        /// <summary>
        /// Attempts to get the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the object from the cache that has the specified key, or the default value of the type if the operation failed.</param>
        /// <returns>true if the key was found in the cache; otherwise, false.</returns>
        bool TryGet(K key, out V value);

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to generate a value for the key.</param>
        /// <returns>The value for the key. This will be either the existing value for the key if the key is already 
        /// in the cache, or the new value if the key was not in the cache.</returns>
        V GetOrAdd(K key, Func<K, V> valueFactory);

        // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to generate a value for the key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>The value for the key. This will be either the existing value for the key if the key is already 
        /// in the cache, or the new value if the key was not in the cache.</returns>
        V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument) => this.GetOrAdd(key, k => valueFactory(k, factoryArgument));

        /// <summary>
        /// Attempts to remove and return the value that has the specified key.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">When this method returns, contains the object removed, or the default value of the value type if key does not exist.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        bool TryRemove(K key, out V value) => throw new NotSupportedException();

        /// <summary>
        /// Attempts to remove 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool TryRemove(KeyValuePair<K, V> item) => throw new NotSupportedException();
#endif

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
