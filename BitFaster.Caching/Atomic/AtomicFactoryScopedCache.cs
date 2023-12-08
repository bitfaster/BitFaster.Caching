﻿using System;
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
