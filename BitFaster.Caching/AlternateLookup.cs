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
    public unsafe struct AlternateLookup<TAlternateKey, TKey, TValue>
        where TAlternateKey : notnull, allows ref struct
        where TKey : notnull
    {
        private delegate* managed<ref AlternateLookupStorage, TAlternateKey, out TValue, bool> tryGet;
        private delegate* managed<ref AlternateLookupStorage, TAlternateKey, out TKey, out TValue, bool> tryRemove;
        private delegate* managed<ref AlternateLookupStorage, TAlternateKey, TValue, bool> tryUpdate;
        private delegate* managed<ref AlternateLookupStorage, TAlternateKey, TValue, void> addOrUpdate;
        private delegate* managed<ref AlternateLookupStorage, TAlternateKey, GetOrAddFactory<TKey, TValue>, TValue> getOrAdd;
        private AlternateLookupStorage storage;

        internal static AlternateLookup<TAlternateKey, TKey, TValue> Create<TLookup>(TLookup lookup)
            where TLookup : struct, IAlternateLookup<TAlternateKey, TKey, TValue>
        {
            if (Unsafe.SizeOf<TLookup>() > AlternateLookupStorage.Size)
            {
                Throw.InvalidOp($"Alternate lookup storage is too small for {typeof(TLookup)}.");
            }

            AlternateLookup<TAlternateKey, TKey, TValue> alternateLookup = default;
            Unsafe.As<AlternateLookupStorage, TLookup>(ref alternateLookup.storage) = lookup;
            alternateLookup.tryGet = &AlternateLookupDispatch<TAlternateKey, TKey, TValue, TLookup>.TryGet;
            alternateLookup.tryRemove = &AlternateLookupDispatch<TAlternateKey, TKey, TValue, TLookup>.TryRemove;
            alternateLookup.tryUpdate = &AlternateLookupDispatch<TAlternateKey, TKey, TValue, TLookup>.TryUpdate;
            alternateLookup.addOrUpdate = &AlternateLookupDispatch<TAlternateKey, TKey, TValue, TLookup>.AddOrUpdate;
            alternateLookup.getOrAdd = &AlternateLookupDispatch<TAlternateKey, TKey, TValue, TLookup>.GetOrAdd;
            return alternateLookup;
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
            this.ThrowIfDefault();
            return this.tryGet(ref this.storage, key, out value);
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
            this.ThrowIfDefault();
            return this.tryRemove(ref this.storage, key, out actualKey, out value);
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
            this.ThrowIfDefault();
            return this.tryUpdate(ref this.storage, key, value);
        }

        /// <summary>
        /// Adds a value using an alternate key or updates the existing value.
        /// </summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="value">The value to add or update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdate(TAlternateKey key, TValue value)
        {
            this.ThrowIfDefault();
            this.addOrUpdate(ref this.storage, key, value);
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
            this.ThrowIfDefault();

            var state = valueFactory;
            var factory = new GetOrAddFactory<TKey, TValue>(&GetOrAddFuncFactoryDispatch<TKey, TValue>.Invoke, (nint)Unsafe.AsPointer(ref state));
            return this.getOrAdd(ref this.storage, key, factory);
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
            this.ThrowIfDefault();

            var state = new GetOrAddFuncFactoryState<TKey, TValue, TArg>(valueFactory, factoryArgument);
            var factory = new GetOrAddFactory<TKey, TValue>(&GetOrAddFuncFactoryDispatch<TKey, TValue, TArg>.Invoke, (nint)Unsafe.AsPointer(ref state));
            return this.getOrAdd(ref this.storage, key, factory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDefault()
        {
            if (this.tryGet == null)
            {
                Throw.InvalidOp("Alternate lookup is not initialized.");
            }
        }

        [InlineArray(24)]
        internal struct AlternateLookupStorage
        {
            internal static readonly int Size = 24 * IntPtr.Size;

            private nint element0;
        }
    }

    internal unsafe readonly struct GetOrAddFactory<TKey, TValue>
        where TKey : notnull
    {
        private readonly delegate* managed<TKey, nint, TValue> invoke;
        private readonly nint state;

        internal GetOrAddFactory(delegate* managed<TKey, nint, TValue> invoke, nint state)
        {
            this.invoke = invoke;
            this.state = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TValue Invoke(TKey key)
        {
            return this.invoke(key, this.state);
        }
    }

    internal static class AlternateLookupDispatch<TAlternateKey, TKey, TValue, TLookup>
        where TAlternateKey : notnull, allows ref struct
        where TKey : notnull
        where TLookup : struct, IAlternateLookup<TAlternateKey, TKey, TValue>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGet(ref AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage storage, TAlternateKey key, out TValue value)
        {
#pragma warning disable CS8601
            return Unsafe.As<AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage, TLookup>(ref storage).TryGet(key, out value);
#pragma warning restore CS8601
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryRemove(ref AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage storage, TAlternateKey key, out TKey actualKey, out TValue value)
        {
#pragma warning disable CS8601
            return Unsafe.As<AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage, TLookup>(ref storage).TryRemove(key, out actualKey, out value);
#pragma warning restore CS8601
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryUpdate(ref AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage storage, TAlternateKey key, TValue value)
        {
            return Unsafe.As<AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage, TLookup>(ref storage).TryUpdate(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddOrUpdate(ref AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage storage, TAlternateKey key, TValue value)
        {
            Unsafe.As<AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage, TLookup>(ref storage).AddOrUpdate(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TValue GetOrAdd(ref AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage storage, TAlternateKey key, GetOrAddFactory<TKey, TValue> valueFactory)
        {
            return Unsafe.As<AlternateLookup<TAlternateKey, TKey, TValue>.AlternateLookupStorage, TLookup>(ref storage).GetOrAdd(key, static (actualKey, factory) => factory.Invoke(actualKey), valueFactory);
        }
    }

    internal unsafe static class GetOrAddFuncFactoryDispatch<TKey, TValue>
        where TKey : notnull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TValue Invoke(TKey key, nint state)
        {
            return Unsafe.AsRef<Func<TKey, TValue>>((void*)state)(key);
        }
    }

    internal readonly struct GetOrAddFuncFactoryState<TKey, TValue, TArg>
        where TKey : notnull
    {
        internal readonly Func<TKey, TArg, TValue> ValueFactory;
        internal readonly TArg FactoryArgument;

        internal GetOrAddFuncFactoryState(Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument)
        {
            this.ValueFactory = valueFactory;
            this.FactoryArgument = factoryArgument;
        }
    }

    internal unsafe static class GetOrAddFuncFactoryDispatch<TKey, TValue, TArg>
        where TKey : notnull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TValue Invoke(TKey key, nint state)
        {
            ref readonly var localState = ref Unsafe.AsRef<GetOrAddFuncFactoryState<TKey, TValue, TArg>>((void*)state);
            return localState.ValueFactory(key, localState.FactoryArgument);
        }
    }
}
#endif
