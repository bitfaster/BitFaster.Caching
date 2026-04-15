#if NET9_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;

namespace BitFaster.Caching
{
    /// <summary>
    /// Provides an alternate-key lookup over a scoped cache.
    /// </summary>
    /// <typeparam name="TAlternateKey">The alternate key type.</typeparam>
    /// <typeparam name="TKey">The cache key type.</typeparam>
    /// <typeparam name="TValue">The cache value type.</typeparam>
    public interface IScopedAlternateLookup<TAlternateKey, TKey, TValue>
        where TAlternateKey : notnull, allows ref struct
        where TKey : notnull
        where TValue : IDisposable
    {
        /// <summary>
        /// Attempts to get a value lifetime using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="lifetime">The value lifetime when found.</param>
        /// <returns><see langword="true" /> when the key is found; otherwise, <see langword="false" />.</returns>
        bool ScopedTryGet(TAlternateKey key, [MaybeNullWhen(false)] out Lifetime<TValue> lifetime);

        /// <summary>
        /// Attempts to remove a value using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="actualKey">The removed cache key.</param>
        /// <returns><see langword="true" /> when the key is found; otherwise, <see langword="false" />.</returns>
        bool TryRemove(TAlternateKey key, [MaybeNullWhen(false)] out TKey actualKey);

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
        /// Gets an existing value lifetime or adds a new value using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="valueFactory">The value factory, invoked with the actual cache key when a value must be created.</param>
        /// <returns>The cached value lifetime.</returns>
        Lifetime<TValue> ScopedGetOrAdd(TAlternateKey key, Func<TKey, Scoped<TValue>> valueFactory);

        /// <summary>
        /// Gets an existing value lifetime or adds a new value using an alternate key and factory argument.
        /// </summary>
        /// <typeparam name="TArg">The factory argument type.</typeparam>
        /// <param name="key">The alternate key.</param>
        /// <param name="valueFactory">The value factory, invoked with the actual cache key when a value must be created.</param>
        /// <param name="factoryArgument">The factory argument.</param>
        /// <returns>The cached value lifetime.</returns>
        Lifetime<TValue> ScopedGetOrAdd<TArg>(TAlternateKey key, Func<TKey, TArg, Scoped<TValue>> valueFactory, TArg factoryArgument);
    }
}
#endif
