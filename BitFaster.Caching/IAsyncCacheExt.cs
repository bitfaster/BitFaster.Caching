using System;
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
    /// <remarks>This interface enables .NET Standard to use cache methods added to IAsyncCache since v2.0. It will be removed in the next major version.</remarks>
    public interface IAsyncCacheExt<K, V> : IAsyncCache<K, V>
    {
        // Following methods were also defined in ICache with default interface implementation which only works for
        // certain build targets, for other build targets we will define them within this new interface to avoid breaking
        // existing clients.
// backcompat: remove conditional compile
#if NETSTANDARD
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
        ValueTask<V> GetOrAddAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument);

        /// <summary>
        /// Attempts to remove and return the value that has the specified key.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">When this method returns, contains the object removed, or the default value of the value type if key does not exist.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        bool TryRemove(K key, [MaybeNullWhen(false)] out V value);

        /// <summary>
        /// Attempts to remove the specified key value pair.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>true if the item was removed successfully; otherwise, false.</returns>
        bool TryRemove(KeyValuePair<K, V> item);
#endif
    }
}
