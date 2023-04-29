using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a generic cache of key/scoped IDisposable value pairs.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    public interface IScopedAsyncCache<K, V> : IEnumerable<KeyValuePair<K, Scoped<V>>> where V : IDisposable
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
        /// <remarks>
        /// Events expose the Scoped instance wrapping each value. To keep the value alive (blocking Dispose), try to 
        /// create a Lifetime from the scope.
        /// </remarks>
        Optional<ICacheEvents<K, Scoped<V>>> Events { get; }

        /// <summary>
        /// Gets the cache policy.
        /// </summary>
        CachePolicy Policy { get; }

        /// <summary>
        /// Gets a collection containing the keys in the cache.
        /// </summary>
        ICollection<K> Keys { get; }

        /// <summary>
        /// Attempts to create a lifetime for the value associated with the specified key from the cache
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="lifetime">When this method returns, contains a lifetime for the object from the cache that 
        /// has the specified key, or the default value of the type if the operation failed.</param>
        /// <returns>true if the key was found in the cache; otherwise, false.</returns>
        bool ScopedTryGet(K key, out Lifetime<V> lifetime);

        /// <summary>
        /// Adds a key/scoped value pair to the cache if the key does not already exist. Returns a lifetime for either 
        /// the new value, or the existing value if the key already exists.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a scoped value for the key.</param>
        /// <returns>A task that represents the asynchronous ScopedGetOrAdd operation.</returns>
        ValueTask<Lifetime<V>> ScopedGetOrAddAsync(K key, Func<K, Task<Scoped<V>>> valueFactory);

        // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Adds a key/scoped value pair to the cache if the key does not already exist. Returns a lifetime for either 
        /// the new value, or the existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a scoped value for the key.</param>
        /// <param name="factoryArgument"></param>
        /// <returns>A task that represents the asynchronous ScopedGetOrAdd operation.</returns>
        ValueTask<Lifetime<V>> ScopedGetOrAddAsync<TArg>(K key, Func<K, TArg, Task<Scoped<V>>> valueFactory, TArg factoryArgument) => this.ScopedGetOrAddAsync(key, (k) => valueFactory(k, factoryArgument));
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
