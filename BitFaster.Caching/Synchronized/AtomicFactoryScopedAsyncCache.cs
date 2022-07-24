using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Synchronized
{
    public sealed class AtomicFactoryScopedAsyncCache<K, V> : IScopedAsyncCache<K, V> where V : IDisposable
    {
        private readonly ICache<K, ScopedAsyncAtomicFactory<K, V>> cache;
        private readonly EventProxy eventProxy;

        public AtomicFactoryScopedAsyncCache(ICache<K, ScopedAsyncAtomicFactory<K, V>> cache)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            this.cache = cache;
            this.eventProxy = new EventProxy(cache.Events);
        }

        public int Capacity => this.cache.Capacity;

        public int Count => this.cache.Count;

        public ICacheMetrics Metrics => this.cache.Metrics;

        public ICacheEvents<K, Scoped<V>> Events => this.eventProxy;

        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new ScopedAsyncAtomicFactory<K, V>(value));
        }

        public void Clear()
        {
            this.cache.Clear();
        }

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
                    throw new InvalidOperationException(ScopedCacheDefaults.RetryFailureMessage);
                }
            }
        }

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

        public void Trim(int itemCount)
        {
            this.cache.Trim(itemCount);
        }

        public bool TryRemove(K key)
        {
            return this.cache.TryRemove(key);
        }

        public bool TryUpdate(K key, V value)
        {
            return this.cache.TryUpdate(key, new ScopedAsyncAtomicFactory<K, V>(value));
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
        }
    }
}
