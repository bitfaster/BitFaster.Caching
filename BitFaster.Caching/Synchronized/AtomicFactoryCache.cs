using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Synchronized
{
    public sealed class AtomicFactoryCache<K, V> : ICache<K, V>
    {
        private readonly ICache<K, AtomicFactory<K, V>> cache;
        private readonly EventProxy eventProxy;

        public AtomicFactoryCache(ICache<K, AtomicFactory<K, V>> cache)
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

        public ICacheEvents<K, V> Events => this.eventProxy;

        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new AtomicFactory<K, V>(value));
        }

        public void Clear()
        {
            this.cache.Clear();
        }

        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            var atomicFactory = cache.GetOrAdd(key, _ => new AtomicFactory<K, V>());
            return atomicFactory.GetValue(key, valueFactory);
        }

        public Task<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            throw new NotImplementedException();
        }

        public void Trim(int itemCount)
        {
            this.cache.Trim(itemCount);
        }

        public bool TryGet(K key, out V value)
        {
            AtomicFactory<K, V> output;
            var ret = cache.TryGet(key, out output);

            if (ret && output.IsValueCreated)
            {
                value = output.ValueIfCreated;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryRemove(K key)
        {
            return cache.TryRemove(key);
        }

        public bool TryUpdate(K key, V value)
        {
            return cache.TryUpdate(key, new AtomicFactory<K, V>(value));
        }

        private class EventProxy : CacheEventProxyBase<K, AtomicFactory<K, V>, V>
        {
            public EventProxy(ICacheEvents<K, AtomicFactory<K, V>> inner)
                : base(inner)
            {
            }

            protected override ItemRemovedEventArgs<K, V> TranslateOnRemoved(ItemRemovedEventArgs<K, AtomicFactory<K, V>> inner)
            {
                return new Lru.ItemRemovedEventArgs<K, V>(inner.Key, inner.Value.ValueIfCreated, inner.Reason);
            }
        }
    }
}
