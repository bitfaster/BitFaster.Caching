using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Synchronized
{
    public class AtomicFactoryScopedCache<K, V> : IScopedCache<K, V> where V : IDisposable
    {
        private readonly ICache<K, ScopedAtomicFactory<K, V>> cache;
        private readonly EventsProxy eventsProxy;

        public AtomicFactoryScopedCache(ICache<K, ScopedAtomicFactory<K, V>> cache)
        {
            this.cache = cache;
            this.eventsProxy = new EventsProxy(cache.Events);
        }

        public int Capacity => this.cache.Capacity;

        public int Count => this.cache.Count;

        public ICacheMetrics Metrics => this.cache.Metrics;

        public ICacheEvents<K, Scoped<V>> Events => this.eventsProxy;

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

        public Task<Lifetime<V>> ScopedGetOrAddAsync(K key, Func<K, Task<Scoped<V>>> valueFactory)
        {
            throw new NotImplementedException();
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
            return this.cache.TryUpdate(key, new ScopedAtomicFactory<K, V>(value));
        }

        private class EventsProxy : ICacheEvents<K, Scoped<V>>
        {
            private readonly ICacheEvents<K, ScopedAtomicFactory<K, V>> inner;
            private event EventHandler<Lru.ItemRemovedEventArgs<K, Scoped<V>>> itemRemovedProxy;

            public EventsProxy(ICacheEvents<K, ScopedAtomicFactory<K, V>> inner)
            {
                this.inner = inner;
            }

            public bool IsEnabled => this.inner.IsEnabled;

            public event EventHandler<Lru.ItemRemovedEventArgs<K, Scoped<V>>> ItemRemoved
            {
                add { this.Register(value); }
                remove { this.UnRegister(value); }
            }

            private void Register(EventHandler<Lru.ItemRemovedEventArgs<K, Scoped<V>>> value)
            {
                itemRemovedProxy += value;
                inner.ItemRemoved += OnItemRemoved;
            }

            private void UnRegister(EventHandler<Lru.ItemRemovedEventArgs<K, Scoped<V>>> value)
            {
                this.itemRemovedProxy -= value;

                if (this.itemRemovedProxy.GetInvocationList().Length == 0)
                {
                    this.inner.ItemRemoved -= OnItemRemoved;
                }
            }

            private void OnItemRemoved(object sender, Lru.ItemRemovedEventArgs<K, ScopedAtomicFactory<K, V>> e)
            {
                // forward from inner to outer
                itemRemovedProxy.Invoke(sender, new Lru.ItemRemovedEventArgs<K, Scoped<V>>(e.Key, e.Value.ScopeIfCreated, e.Reason));
            }
        }
    }
}
