#if NET9_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching
{
    /// <summary>
    /// Provides an alternate-key lookup over a cache without exposing the concrete cache-specific implementation.
    /// </summary>
    /// <typeparam name="TAlternateKey">The alternate key type.</typeparam>
    /// <typeparam name="TKey">The cache key type.</typeparam>
    /// <typeparam name="TValue">The cache value type.</typeparam>
    public readonly struct AlternateLookup<TAlternateKey, TKey, TValue>
        where TAlternateKey : notnull, allows ref struct
        where TKey : notnull
    {
        private readonly AlternateLookupHandle<TAlternateKey, TKey, TValue>? handle;

        internal AlternateLookup(AlternateLookupHandle<TAlternateKey, TKey, TValue> handle)
        {
            this.handle = handle;
        }

        internal static AlternateLookup<TAlternateKey, TKey, TValue> Create<TLookup>(TLookup lookup)
            where TLookup : struct, IAlternateLookup<TAlternateKey, TKey, TValue>
        {
            return new(new AlternateLookupHandle<TAlternateKey, TKey, TValue, TLookup>(lookup));
        }

        /// <summary>
        /// Attempts to get a value using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="value">The cached value when found.</param>
        /// <returns><see langword="true" /> when the key is found; otherwise, <see langword="false" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(TAlternateKey key, [MaybeNullWhen(false)] out TValue value)
        {
            return this.GetHandle().TryGet(key, out value);
        }

        /// <summary>
        /// Attempts to remove a value using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="actualKey">The removed cache key.</param>
        /// <param name="value">The removed value.</param>
        /// <returns><see langword="true" /> when the key is found; otherwise, <see langword="false" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemove(TAlternateKey key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value)
        {
            return this.GetHandle().TryRemove(key, out actualKey, out value);
        }

        /// <summary>
        /// Attempts to update an existing value using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="value">The value to update.</param>
        /// <returns><see langword="true" /> when the key was updated; otherwise, <see langword="false" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryUpdate(TAlternateKey key, TValue value)
        {
            return this.GetHandle().TryUpdate(key, value);
        }

        /// <summary>
        /// Adds a value using an alternate key or updates the existing value.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="value">The value to add or update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdate(TAlternateKey key, TValue value)
        {
            this.GetHandle().AddOrUpdate(key, value);
        }

        /// <summary>
        /// Gets an existing value or adds a new value using an alternate key.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="valueFactory">The value factory, invoked with the actual cache key when a value must be created.</param>
        /// <returns>The cached value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetOrAdd(TAlternateKey key, Func<TKey, TValue> valueFactory)
        {
            return this.GetHandle().GetOrAdd(key, valueFactory);
        }

        /// <summary>
        /// Gets an existing value or adds a new value using an alternate key and factory argument.
        /// </summary>
        /// <typeparam name="TArg">The factory argument type.</typeparam>
        /// <param name="key">The alternate key.</param>
        /// <param name="valueFactory">The value factory, invoked with the actual cache key when a value must be created.</param>
        /// <param name="factoryArgument">The factory argument.</param>
        /// <returns>The cached value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetOrAdd<TArg>(TAlternateKey key, Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument)
        {
            return this.GetHandle().GetOrAdd(key, valueFactory, factoryArgument);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AlternateLookupHandle<TAlternateKey, TKey, TValue> GetHandle()
        {
            var local = this.handle;

            if (local is null)
            {
                Throw.InvalidOp("Alternate lookup is not initialized.");
            }

            return local;
        }
    }

    internal abstract class AlternateLookupHandle<TAlternateKey, TKey, TValue>
        where TAlternateKey : notnull, allows ref struct
        where TKey : notnull
    {
        public abstract bool TryGet(TAlternateKey key, [MaybeNullWhen(false)] out TValue value);

        public abstract bool TryRemove(TAlternateKey key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value);

        public abstract bool TryUpdate(TAlternateKey key, TValue value);

        public abstract void AddOrUpdate(TAlternateKey key, TValue value);

        public abstract TValue GetOrAdd(TAlternateKey key, Func<TKey, TValue> valueFactory);

        public abstract TValue GetOrAdd<TArg>(TAlternateKey key, Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument);
    }

    internal sealed class AlternateLookupHandle<TAlternateKey, TKey, TValue, TLookup> : AlternateLookupHandle<TAlternateKey, TKey, TValue>
        where TAlternateKey : notnull, allows ref struct
        where TKey : notnull
        where TLookup : struct, IAlternateLookup<TAlternateKey, TKey, TValue>
    {
        private TLookup lookup;

        internal AlternateLookupHandle(TLookup lookup)
        {
            this.lookup = lookup;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool TryGet(TAlternateKey key, [MaybeNullWhen(false)] out TValue value)
        {
            return this.lookup.TryGet(key, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool TryRemove(TAlternateKey key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value)
        {
            return this.lookup.TryRemove(key, out actualKey, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool TryUpdate(TAlternateKey key, TValue value)
        {
            return this.lookup.TryUpdate(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddOrUpdate(TAlternateKey key, TValue value)
        {
            this.lookup.AddOrUpdate(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override TValue GetOrAdd(TAlternateKey key, Func<TKey, TValue> valueFactory)
        {
            return this.lookup.GetOrAdd(key, valueFactory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override TValue GetOrAdd<TArg>(TAlternateKey key, Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument)
        {
            return this.lookup.GetOrAdd(key, valueFactory, factoryArgument);
        }
    }
}
#endif
