﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a generic cache of key/value pairs.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    public interface IAsyncCache<K, V> : IEnumerable<KeyValuePair<K, V>>
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
        bool TryGet(K key, [MaybeNullWhen(false)] out V value);

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a value for the key.</param>
        /// <returns>A task that represents the asynchronous GetOrAdd operation.</returns>
        ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory);

// backcompat: remove conditional compile
#if !NETSTANDARD
        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a value for the key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>A task that represents the asynchronous GetOrAdd operation.</returns>
        /// <remarks>The default implementation given here is the fallback that provides backwards compatibility for classes that implement ICache on prior versions</remarks>
        ValueTask<V> GetOrAddAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument) => this.GetOrAddAsync(key, k => valueFactory(k, factoryArgument));

        /// <summary>
        /// Attempts to remove and return the value that has the specified key.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">When this method returns, contains the object removed, or the default value of the value type if key does not exist.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        bool TryRemove(K key, [MaybeNullWhen(false)] out V value) => throw new NotSupportedException();

        /// <summary>
        /// Attempts to remove the specified key value pair.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>true if the item was removed successfully; otherwise, false.</returns>
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
