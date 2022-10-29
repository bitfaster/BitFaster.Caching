using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A cache decorator for working with  <see cref="ScopedAsyncAtomicFactory{K, V}"/> wrapped values, giving exactly once initialization.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
    public sealed class AtomicFactoryScopedAsyncCache<K, V> : IScopedAsyncCache<K, V> where V : IDisposable
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
            {
                Ex.ThrowArgNull(ExceptionArgument.cache);
            }

            this.cache = cache;
            if (cache.Events.HasValue)
            {
                this.events = new Optional<ICacheEvents<K, Scoped<V>>>(new EventProxy(cache.Events.Value));
            }
            else
            {
                this.events = Optional<ICacheEvents<K, Scoped<V>>>.None();
            }
        }

        ///<inheritdoc/>
        public int Count => this.cache.Count;

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => this.cache.Metrics;

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, Scoped<V>>> Events => this.events;

        ///<inheritdoc/>
        public CachePolicy Policy => this.cache.Policy;

        ///<inheritdoc/>
        public ICollection<K> Keys => this.cache.Keys;

        ///<inheritdoc/>
        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new ScopedAsyncAtomicFactory<K, V>(value));
        }

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
                var scope = cache.GetOrAdd(key, _ => new ScopedAsyncAtomicFactory<K, V>());

                var result = await scope.TryCreateLifetimeAsync(key, valueFactory).ConfigureAwait(false);

                if (result.success)
                {
                    return result.lifetime;
                }

                spinwait.SpinOnce();

                if (c++ > ScopedCacheDefaults.MaxRetry)
                {
                    Ex.ThrowScopedRetryFailure();
                }
            }
        }

        ///<inheritdoc/>
        public bool ScopedTryGet(K key, out Lifetime<V> lifetime)
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

        ///<inheritdoc/>
        public bool TryUpdate(K key, V value)
        {
            return this.cache.TryUpdate(key, new ScopedAsyncAtomicFactory<K, V>(value));
        }

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<K, Scoped<V>>> GetEnumerator()
        {
            foreach (var kvp in this.cache)
            {
                yield return new KeyValuePair<K, Scoped<V>>(kvp.Key, kvp.Value.ScopeIfCreated);
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
                return new ItemRemovedEventArgs<K, Scoped<V>>(inner.Key, inner.Value.ScopeIfCreated, inner.Reason);
            }

            protected override ItemUpdatedEventArgs<K, Scoped<V>> TranslateOnUpdated(ItemUpdatedEventArgs<K, ScopedAsyncAtomicFactory<K, V>> inner)
            {
                return new ItemUpdatedEventArgs<K, Scoped<V>>(inner.Key, inner.OldValue.ScopeIfCreated, inner.NewValue.ScopeIfCreated);
            }
        }
    }
}
