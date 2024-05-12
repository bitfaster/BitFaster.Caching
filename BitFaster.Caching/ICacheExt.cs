using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a generic cache of key/value pairs. This is a new interface with new methods to avoid breaking backward compatibility.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    /// <remarks>This interface enables .NET Standard to use cache methods added to ICache since v2.0. It will be removed in the next major version.</remarks>
    public interface ICacheExt<K, V> : ICache<K, V>
    {
        // Following methods were also defined in ICache with default interface implementation which only works for
        // certain build targets, for other build targets we will define them within this new interface to avoid breaking
        // existing clients.
// backcompat: remove conditional compile
#if !NETCOREAPP3_0_OR_GREATER
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
        V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument);

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
