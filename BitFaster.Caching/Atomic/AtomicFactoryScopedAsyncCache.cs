using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A cache decorator for working with  <see cref="ScopedAsyncAtomicFactory{K, V}"/> wrapped values, giving exactly once initialization.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    [DebuggerTypeProxy(typeof(ScopedAsyncCacheDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    public sealed class AtomicFactoryScopedAsyncCache<K, V> : IScopedAsyncCache<K, V>
        where K : notnull
        where V : IDisposable
    {
        private readonly ICache<K, ScopedAsyncAtomicFactory<K, V>> cache;
        private readonly Optional<ICacheEvents<K, Scoped<V>>> events;

        /// <summary>
        /// Initializes a new instance of the AtomicFactoryScopedAsyncCache class with the specified inner cache.
        /// </summary>
        /// <param name="cache">The decorated cache.</param>
        public AtomicFactoryScopedAsyncCache(ICache<K, ScopedAsyncAtomicFactory<K, V>> cache)
        {
            if (cache == null)
                Throw.ArgNull(ExceptionArgument.cache);

            this.cache = cache;
            if (cache.Events.HasValue)
            {
                this.events = new Optional<ICacheEvents<K, Scoped<V>>>(new EventProxy(cache.Events.Value!));
            }
            else
            {
                this.events = Optional<ICacheEvents<K, Scoped<V>>>.None();
            }
        }

        ///<inheritdoc/>
        public int Count => AtomicEx.EnumerateCount(this.GetEnumerator());

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => this.cache.Metrics;

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, Scoped<V>>> Events => this.events;

        ///<inheritdoc/>
        public CachePolicy Policy => this.cache.Policy;

        ///<inheritdoc/>
        public ICollection<K> Keys => AtomicEx.FilterKeys<K, ScopedAsyncAtomicFactory<K, V>>(this.cache, v => v.IsScopeCreated);

#if NET9_0_OR_GREATER
        ///<inheritdoc/>
        public IEqualityComparer<K> Comparer => this.cache.Comparer;
#endif

#pragma warning disable CA2000 // Dispose objects before losing scope
        ///<inheritdoc/>
        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new ScopedAsyncAtomicFactory<K, V>(value));
        }
#pragma warning restore CA2000 // Dispose objects before losing scope

        ///<inheritdoc/>
        public void Clear()
        {
            this.cache.Clear();
        }

        ///<inheritdoc/>
        public async ValueTask<Lifetime<V>> ScopedGetOrAddAsync(K key, Func<K, Task<Scoped<V>>> valueFactory)
        {
            return await ScopedGetOrAddAsync(key, new AsyncValueFactory<K, Scoped<V>>(valueFactory)).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a key/scoped value pair to the cache if the key does not already exist. Returns a lifetime for either 
        /// the new value, or the existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a scoped value for the key.</param>
        /// <param name="factoryArgument"></param>
        /// <returns>A task that represents the asynchronous ScopedGetOrAdd operation.</returns>
        public async ValueTask<Lifetime<V>> ScopedGetOrAddAsync<TArg>(K key, Func<K, TArg, Task<Scoped<V>>> valueFactory, TArg factoryArgument)
        {
            return await ScopedGetOrAddAsync(key, new AsyncValueFactoryArg<K, TArg, Scoped<V>>(valueFactory, factoryArgument)).ConfigureAwait(false);
        }

        private async ValueTask<Lifetime<V>> ScopedGetOrAddAsync<TFactory>(K key, TFactory valueFactory) where TFactory : struct, IAsyncValueFactory<K, Scoped<V>>
        {
            int c = 0;
            var spinwait = new SpinWait();
            while (true)
            {
                var scope = cache.GetOrAdd(key, _ => new ScopedAsyncAtomicFactory<K, V>());

                var (success, lifetime) = await scope.TryCreateLifetimeAsync(key, valueFactory).ConfigureAwait(false);

                if (success)
                {
                    return lifetime!;
                }

                spinwait.SpinOnce();

                if (c++ > ScopedCacheDefaults.MaxRetry)
                    Throw.ScopedRetryFailure();
            }
        }

        ///<inheritdoc/>
        public bool ScopedTryGet(K key, [MaybeNullWhen(false)] out Lifetime<V> lifetime)
        {
            if (this.cache.TryGet(key, out var scope))
            {
                if (scope.TryCreateLifetime(out lifetime))
                {
                    return true;
                }
            }

            lifetime = default;
            return false;
        }

        ///<inheritdoc/>
        public bool TryRemove(K key)
        {
            return this.cache.TryRemove(key);
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        ///<inheritdoc/>
        public bool TryUpdate(K key, V value)
        {
            return this.cache.TryUpdate(key, new ScopedAsyncAtomicFactory<K, V>(value));
        }
#pragma warning restore CA2000 // Dispose objects before losing scope

#if NET9_0_OR_GREATER
        ///<inheritdoc/>
        public IScopedAsyncAlternateLookup<TAlternateKey, K, V> GetAsyncAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            var inner = this.cache.GetAlternateLookup<TAlternateKey>();
            var comparer = (IAlternateEqualityComparer<TAlternateKey, K>)this.cache.Comparer;
            return new AlternateLookup<TAlternateKey>(this.cache, inner, comparer);
        }

        ///<inheritdoc/>
        public bool TryGetAsyncAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IScopedAsyncAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            if (this.cache.TryGetAlternateLookup<TAlternateKey>(out var inner))
            {
                var comparer = (IAlternateEqualityComparer<TAlternateKey, K>)this.cache.Comparer;
                lookup = new AlternateLookup<TAlternateKey>(this.cache, inner, comparer);
                return true;
            }

            lookup = default;
            return false;
        }

        internal readonly struct AlternateLookup<TAlternateKey> : IScopedAsyncAlternateLookup<TAlternateKey, K, V>
            where TAlternateKey : notnull, allows ref struct
        {
            private readonly ICache<K, ScopedAsyncAtomicFactory<K, V>> cache;
            private readonly IAlternateLookup<TAlternateKey, K, ScopedAsyncAtomicFactory<K, V>> inner;
            private readonly IAlternateEqualityComparer<TAlternateKey, K> comparer;

            internal AlternateLookup(ICache<K, ScopedAsyncAtomicFactory<K, V>> cache, IAlternateLookup<TAlternateKey, K, ScopedAsyncAtomicFactory<K, V>> inner, IAlternateEqualityComparer<TAlternateKey, K> comparer)
            {
                this.cache = cache;
                this.inner = inner;
                this.comparer = comparer;
            }

            public bool ScopedTryGet(TAlternateKey key, [MaybeNullWhen(false)] out Lifetime<V> lifetime)
            {
                if (this.inner.TryGet(key, out var scope) && scope.TryCreateLifetime(out lifetime))
                {
                    return true;
                }

                lifetime = default;
                return false;
            }

            public bool TryRemove(TAlternateKey key, [MaybeNullWhen(false)] out K actualKey)
            {
                if (this.inner.TryRemove(key, out actualKey, out _))
                {
                    return true;
                }

                actualKey = default;
                return false;
            }

#pragma warning disable CA2000 // Dispose objects before losing scope
            public bool TryUpdate(TAlternateKey key, V value)
            {
                return this.inner.TryUpdate(key, new ScopedAsyncAtomicFactory<K, V>(value));
            }

            public void AddOrUpdate(TAlternateKey key, V value)
            {
                this.inner.AddOrUpdate(key, new ScopedAsyncAtomicFactory<K, V>(value));
            }
#pragma warning restore CA2000 // Dispose objects before losing scope

#pragma warning disable CA2000 // Dispose objects before losing scope
            public ValueTask<Lifetime<V>> ScopedGetOrAddAsync(TAlternateKey key, Func<K, Task<Scoped<V>>> valueFactory)
            {
                var scope = this.inner.GetOrAdd(key, static _ => new ScopedAsyncAtomicFactory<K, V>());

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return new ValueTask<Lifetime<V>>(lifetime);
                }

                return ScopedGetOrAddAsync(key, new AsyncValueFactory<K, Scoped<V>>(valueFactory));
            }

            public ValueTask<Lifetime<V>> ScopedGetOrAddAsync<TArg>(TAlternateKey key, Func<K, TArg, Task<Scoped<V>>> valueFactory, TArg factoryArgument)
            {
                var scope = this.inner.GetOrAdd(key, static _ => new ScopedAsyncAtomicFactory<K, V>());

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return new ValueTask<Lifetime<V>>(lifetime);
                }

                return ScopedGetOrAddAsync(key, new AsyncValueFactoryArg<K, TArg, Scoped<V>>(valueFactory, factoryArgument));
            }
#pragma warning restore CA2000 // Dispose objects before losing scope

            private ValueTask<Lifetime<V>> ScopedGetOrAddAsync<TFactory>(TAlternateKey key, TFactory valueFactory) where TFactory : struct, IAsyncValueFactory<K, Scoped<V>>
            {
                K actualKey = this.comparer.Create(key);
                return CompleteAsync(this.cache, actualKey, valueFactory);

                static async ValueTask<Lifetime<V>> CompleteAsync(ICache<K, ScopedAsyncAtomicFactory<K, V>> cache, K actualKey, TFactory valueFactory)
                {
                    int c = 0;
                    var spinwait = new SpinWait();

                    while (true)
                    {
                        var scope = cache.GetOrAdd(actualKey, static _ => new ScopedAsyncAtomicFactory<K, V>());

                        var (success, lifetime) = await scope.TryCreateLifetimeAsync(actualKey, valueFactory).ConfigureAwait(false);

                        if (success)
                        {
                            return lifetime!;
                        }

                        spinwait.SpinOnce();

                        if (c++ > ScopedCacheDefaults.MaxRetry)
                            Throw.ScopedRetryFailure();
                    }
                }
            }
        }
#endif

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<K, Scoped<V>>> GetEnumerator()
        {
            foreach (var kvp in this.cache)
            {
                if (kvp.Value.IsScopeCreated)
                {
                    yield return new KeyValuePair<K, Scoped<V>>(kvp.Key, kvp.Value.ScopeIfCreated!);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((AtomicFactoryScopedAsyncCache<K, V>)this).GetEnumerator();
        }

        private class EventProxy : CacheEventProxyBase<K, ScopedAsyncAtomicFactory<K, V>, Scoped<V>>
        {
            public EventProxy(ICacheEvents<K, ScopedAsyncAtomicFactory<K, V>> inner)
                : base(inner)
            {
            }

            protected override ItemRemovedEventArgs<K, Scoped<V>> TranslateOnRemoved(ItemRemovedEventArgs<K, ScopedAsyncAtomicFactory<K, V>> inner)
            {
                return new ItemRemovedEventArgs<K, Scoped<V>>(inner.Key, inner.Value!.ScopeIfCreated, inner.Reason);
            }

            protected override ItemUpdatedEventArgs<K, Scoped<V>> TranslateOnUpdated(ItemUpdatedEventArgs<K, ScopedAsyncAtomicFactory<K, V>> inner)
            {
                return new ItemUpdatedEventArgs<K, Scoped<V>>(inner.Key, inner.OldValue!.ScopeIfCreated, inner.NewValue!.ScopeIfCreated);
            }
        }
    }
}
