using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public class AtomicCacheDecorator<K, V> : ICache<K, V>
    {
        private readonly ICache<K, AsyncAtomic<K, V>> cache;

        public AtomicCacheDecorator(ICache<K, AsyncAtomic<K, V>> cache)
        {
            this.cache = cache;
        }

        public int Capacity => this.cache.Capacity;

        public int Count => this.cache.Count;

        public ICacheMetrics Metrics => this.cache.Metrics;

        // need to dispatch different events for this
        public ICacheEvents<K, V> Events => throw new Exception();

        public void AddOrUpdate(K key, V value)
        {
            cache.AddOrUpdate(key, new AsyncAtomic<K, V>(value));
        }

        public void Clear()
        {
            this.cache.Clear();
        }

        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            var synchronized = cache.GetOrAdd(key, _ => new AsyncAtomic<K, V>());
            return synchronized.GetValue(key, valueFactory);
        }

        public Task<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            var synchronized = cache.GetOrAdd(key, _ => new AsyncAtomic<K, V>());
            return synchronized.GetValueAsync(key, valueFactory);
        }

        public void Trim(int itemCount)
        {
            this.cache.Trim(itemCount);
        }

        public bool TryGet(K key, out V value)
        {
            AsyncAtomic<K, V> output;
            bool ret = cache.TryGet(key, out output);

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
            return this.cache.TryRemove(key);
        }

        public bool TryUpdate(K key, V value)
        {
            return cache.TryUpdate(key, new AsyncAtomic<K, V>(value)); ;
        }
    }
}
