using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// A cache decorator for working with Scoped IDisposable values. The Scoped methods (e.g. ScopedGetOrAdd)
    /// are threadsafe and create lifetimes that guarantee the value will not be disposed until the
    /// lifetime is disposed.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    [DebuggerTypeProxy(typeof(ScopedAsyncCacheDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    public sealed class ScopedAsyncCache<K, V> : IScopedAsyncCache<K, V>
        where K : notnull
        where V : IDisposable
    {
        private readonly IAsyncCache<K, Scoped<V>> cache;

        /// <summary>
        /// Initializes a new instance of the ScopedAsyncCache class with the specified inner cache.
        /// </summary>
        /// <param name="cache">The decorated cache.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ScopedAsyncCache(IAsyncCache<K, Scoped<V>> cache)
        {
            if (cache == null)
                Throw.ArgNull(ExceptionArgument.cache);

            this.cache = cache;
        }

        ///<inheritdoc/>
        public int Count => this.cache.Count;

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => this.cache.Metrics;

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, Scoped<V>>> Events => this.cache.Events;

        ///<inheritdoc/>
        public CachePolicy Policy => this.cache.Policy;

        ///<inheritdoc/>
        public ICollection<K> Keys => this.cache.Keys;

#if NET9_0_OR_GREATER
        ///<inheritdoc/>
        public IEqualityComparer<K> Comparer => this.cache.Comparer;
#endif

#pragma warning disable CA2000 // Dispose objects before losing scope
        ///<inheritdoc/>
        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new Scoped<V>(value));
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
            int c = 0;
            var spinwait = new SpinWait();
            while (true)
            {
                var scope = await cache.GetOrAddAsync(key, valueFactory).ConfigureAwait(false);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }

                spinwait.SpinOnce();

                if (c++ > ScopedCacheDefaults.MaxRetry)
                    Throw.ScopedRetryFailure();
            }
        }

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
        public async ValueTask<Lifetime<V>> ScopedGetOrAddAsync<TArg>(K key, Func<K, TArg, Task<Scoped<V>>> valueFactory, TArg factoryArgument)
        {
            int c = 0;
            var spinwait = new SpinWait();
            while (true)
            {
                var scope = await cache.GetOrAddAsync(key, valueFactory, factoryArgument).ConfigureAwait(false);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }

                spinwait.SpinOnce();

                if (c++ > ScopedCacheDefaults.MaxRetry)
                    Throw.ScopedRetryFailure();
            }
        }
#endif

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
            return this.cache.TryUpdate(key, new Scoped<V>(value));
        }
#pragma warning restore CA2000 // Dispose objects before losing scope

#if NET9_0_OR_GREATER
        ///<inheritdoc/>
        public IScopedAsyncAlternateLookup<TAlternateKey, K, V> GetAsyncAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            var inner = this.cache.GetAsyncAlternateLookup<TAlternateKey>();
            var comparer = (IAlternateEqualityComparer<TAlternateKey, K>)this.cache.Comparer;
            return new AlternateLookup<TAlternateKey>(this.cache, inner, comparer);
        }

        ///<inheritdoc/>
        public bool TryGetAsyncAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IScopedAsyncAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            if (this.cache.TryGetAsyncAlternateLookup<TAlternateKey>(out var inner))
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
            private readonly IAsyncCache<K, Scoped<V>> cache;
            private readonly IAsyncAlternateLookup<TAlternateKey, K, Scoped<V>> inner;
            private readonly IAlternateEqualityComparer<TAlternateKey, K> comparer;

            internal AlternateLookup(IAsyncCache<K, Scoped<V>> cache, IAsyncAlternateLookup<TAlternateKey, K, Scoped<V>> inner, IAlternateEqualityComparer<TAlternateKey, K> comparer)
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
                return this.inner.TryUpdate(key, new Scoped<V>(value));
            }

            public void AddOrUpdate(TAlternateKey key, V value)
            {
                this.inner.AddOrUpdate(key, new Scoped<V>(value));
            }
#pragma warning restore CA2000 // Dispose objects before losing scope

            public ValueTask<Lifetime<V>> ScopedGetOrAddAsync(TAlternateKey key, Func<K, Task<Scoped<V>>> valueFactory)
            {
                return ScopedGetOrAddAsync(key, new AsyncValueFactory<K, Scoped<V>>(valueFactory));
            }

            public ValueTask<Lifetime<V>> ScopedGetOrAddAsync<TArg>(TAlternateKey key, Func<K, TArg, Task<Scoped<V>>> valueFactory, TArg factoryArgument)
            {
                return ScopedGetOrAddAsync(key, new AsyncValueFactoryArg<K, TArg, Scoped<V>>(valueFactory, factoryArgument));
            }

            private ValueTask<Lifetime<V>> ScopedGetOrAddAsync<TFactory>(TAlternateKey key, TFactory valueFactory) where TFactory : struct, IAsyncValueFactory<K, Scoped<V>>
            {
                K actualKey = this.comparer.Create(key);
                return CompleteAsync(this.cache, actualKey, valueFactory);

                static async ValueTask<Lifetime<V>> CompleteAsync(IAsyncCache<K, Scoped<V>> cache, K actualKey, TFactory valueFactory)
                {
                    int c = 0;
                    var spinwait = new SpinWait();
                    while (true)
                    {
                        var scope = await cache.GetOrAddAsync(actualKey, static (k, factory) => factory.CreateAsync(k), valueFactory).ConfigureAwait(false);

                        if (scope.TryCreateLifetime(out var lifetime))
                        {
                            return lifetime;
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
                yield return kvp;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((ScopedAsyncCache<K, V>)this).GetEnumerator();
        }
    }
}
