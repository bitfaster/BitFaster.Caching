using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A cache decorator for working with  <see cref="ScopedAtomicFactory{K, V}"/> wrapped values, giving exactly once initialization.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    [DebuggerTypeProxy(typeof(ScopedCacheDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    public sealed class AtomicFactoryScopedCache<K, V> : IScopedCache<K, V>
        where K : notnull
        where V : IDisposable
    {
        private readonly ICache<K, ScopedAtomicFactory<K, V>> cache;
        private readonly Optional<ICacheEvents<K, Scoped<V>>> events;

        /// <summary>
        /// Initializes a new instance of the AtomicFactoryScopedCache class with the specified inner cache.
        /// </summary>
        /// <param name="cache">The decorated cache.</param>
        public AtomicFactoryScopedCache(ICache<K, ScopedAtomicFactory<K, V>> cache)
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
        public Optional<ICacheEvents<K, Scoped<V>>> Events => events;

        ///<inheritdoc/>
        public CachePolicy Policy => this.cache.Policy;

        ///<inheritdoc/>
        public ICollection<K> Keys => AtomicEx.FilterKeys<K, ScopedAtomicFactory<K, V>>(this.cache, v => v.IsScopeCreated);

#if NET9_0_OR_GREATER
        ///<inheritdoc/>
        public IEqualityComparer<K> Comparer => this.cache.Comparer;
#endif

#pragma warning disable CA2000 // Dispose objects before losing scope
        ///<inheritdoc/>
        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new ScopedAtomicFactory<K, V>(value));
        }
#pragma warning restore CA2000 // Dispose objects before losing scope

        ///<inheritdoc/>
        public void Clear()
        {
            this.cache.Clear();
        }

        ///<inheritdoc/>
        public Lifetime<V> ScopedGetOrAdd(K key, Func<K, Scoped<V>> valueFactory)
        {
            return ScopedGetOrAdd(key, new ValueFactory<K, Scoped<V>>(valueFactory));
        }

        /// <summary>
        /// Adds a key/scoped value pair to the cache if the key does not already exist. Returns a lifetime for either 
        /// the new value, or the existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to generate a scoped value for the key.</param>
        /// <param name="factoryArgument"></param>
        /// <returns>The lifetime for the value associated with the key. The lifetime will be either reference the 
        /// existing value for the key if the key is already in the cache, or the new value if the key was not in 
        /// the cache.</returns>
        public Lifetime<V> ScopedGetOrAdd<TArg>(K key, Func<K, TArg, Scoped<V>> valueFactory, TArg factoryArgument)
        {
            return ScopedGetOrAdd(key, new ValueFactoryArg<K, TArg, Scoped<V>>(valueFactory, factoryArgument));
        }

        private Lifetime<V> ScopedGetOrAdd<TFactory>(K key, TFactory valueFactory) where TFactory : struct, IValueFactory<K, Scoped<V>>
        {
            int c = 0;
            var spinwait = new SpinWait();
            while (true)
            {
                var scope = cache.GetOrAdd(key, _ => new ScopedAtomicFactory<K, V>());

                if (scope.TryCreateLifetime(key, valueFactory, out var lifetime))
                {
                    return lifetime;
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
            return this.cache.TryUpdate(key, new ScopedAtomicFactory<K, V>(value));
        }
#pragma warning restore CA2000 // Dispose objects before losing scope

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
            return ((AtomicFactoryScopedCache<K, V>)this).GetEnumerator();
        }

#if NET9_0_OR_GREATER
        ///<inheritdoc/>
        public IScopedAlternateLookup<TAlternateKey, K, V> GetAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            var inner = this.cache.GetAlternateLookup<TAlternateKey>();
            var comparer = (IAlternateEqualityComparer<TAlternateKey, K>)this.cache.Comparer;
            return new AlternateLookup<TAlternateKey>(inner, comparer);
        }

        ///<inheritdoc/>
        public bool TryGetAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IScopedAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            if (this.cache.TryGetAlternateLookup<TAlternateKey>(out var inner))
            {
                var comparer = (IAlternateEqualityComparer<TAlternateKey, K>)this.cache.Comparer;
                lookup = new AlternateLookup<TAlternateKey>(inner, comparer);
                return true;
            }

            lookup = default;
            return false;
        }

        internal readonly struct AlternateLookup<TAlternateKey> : IScopedAlternateLookup<TAlternateKey, K, V>
            where TAlternateKey : notnull, allows ref struct
        {
            private readonly IAlternateLookup<TAlternateKey, K, ScopedAtomicFactory<K, V>> inner;
            private readonly IAlternateEqualityComparer<TAlternateKey, K> comparer;

            internal AlternateLookup(IAlternateLookup<TAlternateKey, K, ScopedAtomicFactory<K, V>> inner, IAlternateEqualityComparer<TAlternateKey, K> comparer)
            {
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
                return this.inner.TryUpdate(key, new ScopedAtomicFactory<K, V>(value));
            }

            public void AddOrUpdate(TAlternateKey key, V value)
            {
                this.inner.AddOrUpdate(key, new ScopedAtomicFactory<K, V>(value));
            }

            public Lifetime<V> ScopedGetOrAdd(TAlternateKey key, Func<K, Scoped<V>> valueFactory)
            {
                var scope = this.inner.GetOrAdd(key, static _ => new ScopedAtomicFactory<K, V>());

                // fast path: create the lifetime without materializing the key
                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }

                return ScopedGetOrAdd(key, new ValueFactory<K, Scoped<V>>(valueFactory));
            }

            public Lifetime<V> ScopedGetOrAdd<TArg>(TAlternateKey key, Func<K, TArg, Scoped<V>> valueFactory, TArg factoryArgument)
            {
                var scope = this.inner.GetOrAdd(key, static _ => new ScopedAtomicFactory<K, V>());

                // fast path: create the lifetime without materializing the key
                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }

                return ScopedGetOrAdd(key, new ValueFactoryArg<K, TArg, Scoped<V>>(valueFactory, factoryArgument));
            }

            private Lifetime<V> ScopedGetOrAdd<TFactory>(TAlternateKey key, TFactory valueFactory) where TFactory : struct, IValueFactory<K, Scoped<V>>
            {
                int c = 0;
                var spinwait = new SpinWait();
                K actualKey = this.comparer.Create(key);

                while (true)
                {
                    var scope = this.inner.GetOrAdd(key, static _ => new ScopedAtomicFactory<K, V>());

                    if (scope.TryCreateLifetime(actualKey, valueFactory, out var lifetime))
                    {
                        return lifetime;
                    }

                    spinwait.SpinOnce();

                    if (c++ > ScopedCacheDefaults.MaxRetry)
                        Throw.ScopedRetryFailure();
                }
            }
#pragma warning restore CA2000 //  Dispose objects before losing scope
        }
#endif

        private class EventProxy : CacheEventProxyBase<K, ScopedAtomicFactory<K, V>, Scoped<V>>
        {
            public EventProxy(ICacheEvents<K, ScopedAtomicFactory<K, V>> inner)
                : base(inner)
            {
            }

            protected override ItemRemovedEventArgs<K, Scoped<V>> TranslateOnRemoved(ItemRemovedEventArgs<K, ScopedAtomicFactory<K, V>> inner)
            {
                return new ItemRemovedEventArgs<K, Scoped<V>>(inner.Key, inner.Value!.ScopeIfCreated, inner.Reason);
            }

            protected override ItemUpdatedEventArgs<K, Scoped<V>> TranslateOnUpdated(ItemUpdatedEventArgs<K, ScopedAtomicFactory<K, V>> inner)
            {
                return new ItemUpdatedEventArgs<K, Scoped<V>>(inner.Key, inner.OldValue!.ScopeIfCreated, inner.NewValue!.ScopeIfCreated);
            }
        }
    }
}
