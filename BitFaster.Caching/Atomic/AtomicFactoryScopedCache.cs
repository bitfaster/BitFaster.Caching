using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Atomic
{
    public sealed class AtomicFactoryScopedCache<K, V> : IScopedCache<K, V> where V : IDisposable
    {
        private readonly ICache<K, ScopedAtomicFactory<K, V>> cache;
        private readonly EventProxy eventProxy;

        public AtomicFactoryScopedCache(ICache<K, ScopedAtomicFactory<K, V>> cache)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            this.cache = cache;
            this.eventProxy = new EventProxy(cache.Events);
        }

        public int Count => this.cache.Count;

        public ICacheMetrics Metrics => this.cache.Metrics;

        public ICacheEvents<K, Scoped<V>> Events => this.eventProxy;

        public CachePolicy Policy => this.cache.Policy;

        ///<inheritdoc/>
        public ICollection<K> Keys => this.cache.Keys;

        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new ScopedAtomicFactory<K, V>(value));
        }

        public void Clear()
        {
            this.cache.Clear();
        }

        public Lifetime<V> ScopedGetOrAdd(K key, Func<K, Scoped<V>> valueFactory)
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

        public bool TryRemove(K key)
        {
            return this.cache.TryRemove(key);
        }

        public bool TryUpdate(K key, V value)
        {
            return this.cache.TryUpdate(key, new ScopedAtomicFactory<K, V>(value));
        }

        public IEnumerator<KeyValuePair<K, Scoped<V>>> GetEnumerator()
        {
            foreach (var kvp in this.cache)
            {
                yield return new KeyValuePair<K, Scoped<V>>(kvp.Key, kvp.Value.ScopeIfCreated);
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
                return new ItemRemovedEventArgs<K, Scoped<V>>(inner.Key, inner.Value.ScopeIfCreated, inner.Reason);
            }
        }
    }
}
