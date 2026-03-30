#if NET9_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// Provides an alternate-key lookup over a scoped async cache.
    /// </summary>
    /// <typeparam name="TAlternateKey">The alternate key type.</typeparam>
    /// <typeparam name="TKey">The cache key type.</typeparam>
    /// <typeparam name="TValue">The cache value type.</typeparam>
    public interface IScopedAsyncAlternateLookup<TAlternateKey, TKey, TValue>
        where TAlternateKey : notnull, allows ref struct
        where TKey : notnull
        where TValue : IDisposable
    {
        /// <summary>
        /// Attempts to create a lifetime for the value associated with the specified alternate key from the cache.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="lifetime">When this method returns, contains a lifetime for the object from the cache that 
        /// has the specified key, or the default value of the type if the operation failed.</param>
        /// <returns>true if the key was found in the cache; otherwise, false.</returns>
        bool ScopedTryGet(TAlternateKey key, [MaybeNullWhen(false)] out Lifetime<TValue> lifetime);

        /// <summary>
        /// Adds a key/scoped value pair to the cache if the key does not already exist using an alternate key for lookup.
        /// Returns a lifetime for either the new value, or the existing value if the key already exists.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a scoped value for the key.</param>
        /// <returns>A task that represents the asynchronous ScopedGetOrAdd operation.</returns>
        ValueTask<Lifetime<TValue>> ScopedGetOrAddAsync(TAlternateKey key, Func<TKey, Task<Scoped<TValue>>> valueFactory);

        /// <summary>
        /// Adds a key/scoped value pair to the cache if the key does not already exist using an alternate key for lookup.
        /// Returns a lifetime for either the new value, or the existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The alternate key.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a scoped value for the key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>A task that represents the asynchronous ScopedGetOrAdd operation.</returns>
        ValueTask<Lifetime<TValue>> ScopedGetOrAddAsync<TArg>(TAlternateKey key, Func<TKey, TArg, Task<Scoped<TValue>>> valueFactory, TArg factoryArgument);

        /// <summary>
        /// Attempts to remove the value that has the specified alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        bool TryRemove(TAlternateKey key);

        /// <summary>
        /// Attempts to update the value that has the specified alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="value">The new value.</param>
        /// <returns>true if the object was updated successfully; otherwise, false.</returns>
        bool TryUpdate(TAlternateKey key, TValue value);

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist, or updates a key/value pair if the 
        /// key already exists, using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="value">The new value.</param>
        void AddOrUpdate(TAlternateKey key, TValue value);
    }
}
#endif
