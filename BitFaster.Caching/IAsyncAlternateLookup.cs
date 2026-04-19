#if NET9_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// Provides an async alternate-key lookup over a cache.
    /// </summary>
    /// <typeparam name="TAlternateKey">The alternate key type.</typeparam>
    /// <typeparam name="TKey">The cache key type.</typeparam>
    /// <typeparam name="TValue">The cache value type.</typeparam>
    public interface IAsyncAlternateLookup<TAlternateKey, TKey, TValue>
        where TAlternateKey : notnull, allows ref struct
        where TKey : notnull
    {
        /// <summary>
        /// Attempts to get a value using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="value">The cached value when found.</param>
        /// <returns><see langword="true" /> when the key is found; otherwise, <see langword="false" />.</returns>
        bool TryGet(TAlternateKey key, [MaybeNullWhen(false)] out TValue value);

        /// <summary>
        /// Attempts to remove a value using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="actualKey">The removed cache key.</param>
        /// <param name="value">The removed value.</param>
        /// <returns><see langword="true" /> when the key is found; otherwise, <see langword="false" />.</returns>
        bool TryRemove(TAlternateKey key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value);

        /// <summary>
        /// Attempts to update an existing value using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="value">The value to update.</param>
        /// <returns><see langword="true" /> when the key was updated; otherwise, <see langword="false" />.</returns>
        bool TryUpdate(TAlternateKey key, TValue value);

        /// <summary>
        /// Adds a value using an alternate key or updates the existing value.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="value">The value to add or update.</param>
        void AddOrUpdate(TAlternateKey key, TValue value);

        /// <summary>
        /// Gets an existing value or adds a new value asynchronously using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a value, invoked with the actual cache key.</param>
        /// <returns>A task that represents the asynchronous GetOrAdd operation.</returns>
        ValueTask<TValue> GetOrAddAsync(TAlternateKey key, Func<TKey, Task<TValue>> valueFactory);

        /// <summary>
        /// Gets an existing value or adds a new value asynchronously using an alternate key and factory argument.
        /// </summary>
        /// <typeparam name="TArg">The factory argument type.</typeparam>
        /// <param name="key">The alternate key.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a value, invoked with the actual cache key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>A task that represents the asynchronous GetOrAdd operation.</returns>
        ValueTask<TValue> GetOrAddAsync<TArg>(TAlternateKey key, Func<TKey, TArg, Task<TValue>> valueFactory, TArg factoryArgument)
            where TArg : allows ref struct;
    }
}
#endif
