using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Atomic
{
    public sealed class AtomicFactoryAsyncCache<K, V> : IAsyncCache<K, V>
    {
        private readonly ICache<K, AsyncAtomicFactory<K, V>> cache;
        private readonly Optional<ICacheEvents<K, V>> events;

        public AtomicFactoryAsyncCache(ICache<K, AsyncAtomicFactory<K, V>> cache)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            this.cache = cache;

            if (cache.Events.HasValue)
            {
                this.events = new Optional<ICacheEvents<K, V>>(new EventProxy(cache.Events.Value));
            }
            else
            {
                this.events = Optional<ICacheEvents<K, V>>.None();
            }
        }

        public int Count => cache.Count;

        public Optional<ICacheMetrics> Metrics => cache.Metrics;

        public Optional<ICacheEvents<K, V>> Events => this.events;

        public ICollection<K> Keys => this.cache.Keys;

        public CachePolicy Policy => this.cache.Policy;

        public void AddOrUpdate(K key, V value)
        {
            cache.AddOrUpdate(key, new AsyncAtomicFactory<K, V>(value));
        }

        public void Clear()
        {
            cache.Clear();
        }

        public ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            var synchronized = cache.GetOrAdd(key, _ => new AsyncAtomicFactory<K, V>());
            return synchronized.GetValueAsync(key, valueFactory);
        }

        public bool TryGet(K key, out V value)
        {
            AsyncAtomicFactory<K, V> output;
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
            return cache.TryUpdate(key, new AsyncAtomicFactory<K, V>(value));
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var kvp in this.cache)
            {
                yield return new KeyValuePair<K, V>(kvp.Key, kvp.Value.ValueIfCreated);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((AtomicFactoryAsyncCache<K, V>)this).GetEnumerator();
        }

        private class EventProxy : CacheEventProxyBase<K, AsyncAtomicFactory<K, V>, V>
        {
            public EventProxy(ICacheEvents<K, AsyncAtomicFactory<K, V>> inner)
                : base(inner)
            {
            }

            protected override ItemRemovedEventArgs<K, V> TranslateOnRemoved(ItemRemovedEventArgs<K, AsyncAtomicFactory<K, V>> inner)
            {
                return new ItemRemovedEventArgs<K, V>(inner.Key, inner.Value.ValueIfCreated, inner.Reason);
            }
        }
    }
}
