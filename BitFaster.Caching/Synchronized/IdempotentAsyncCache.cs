using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Synchronized
{
    public class AtomicCacheDecorator<K, V> : ICache<K, V>
    {
        private readonly ICache<K, AsyncIdempotent<K, V>> cache;

        public AtomicCacheDecorator(ICache<K, AsyncIdempotent<K, V>> cache)
        {
            this.cache = cache;
        }

        public int Capacity => cache.Capacity;

        public int Count => cache.Count;

        public ICacheMetrics Metrics => cache.Metrics;

        // need to dispatch different events for this
        public ICacheEvents<K, V> Events => throw new Exception();

        public void AddOrUpdate(K key, V value)
        {
            cache.AddOrUpdate(key, new AsyncIdempotent<K, V>(value));
        }

        public void Clear()
        {
            cache.Clear();
        }

        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            throw new NotImplementedException();
        }

        public Task<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            var synchronized = cache.GetOrAdd(key, _ => new AsyncIdempotent<K, V>());
            return synchronized.GetValueAsync(key, valueFactory);
        }

        public void Trim(int itemCount)
        {
            cache.Trim(itemCount);
        }

        public bool TryGet(K key, out V value)
        {
            AsyncIdempotent<K, V> output;
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
            return cache.TryUpdate(key, new AsyncIdempotent<K, V>(value)); ;
        }
    }
}
