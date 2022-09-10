using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class MemoryCacheAdaptor<K, V> : ICache<K, V>
    {
        MemoryCacheOptionsAccessor accessor;
        MemoryCache exMemoryCache;

        public MemoryCacheAdaptor(int capacity)
        {
            accessor = new MemoryCacheOptionsAccessor();
            accessor.Value.SizeLimit = capacity;

            exMemoryCache = new MemoryCache(accessor);
        }

        public int Count => throw new NotImplementedException();

        public Optional<ICacheMetrics> Metrics => throw new NotImplementedException();

        public Optional<ICacheEvents<K, V>> Events => throw new NotImplementedException();

        public CachePolicy Policy => throw new NotImplementedException();

        public ICollection<K> Keys => throw new NotImplementedException();

        private static readonly MemoryCacheEntryOptions SizeOne = new MemoryCacheEntryOptions() { Size = 1 };

        public void AddOrUpdate(K key, V value)
        {
            exMemoryCache.Set(key, value, SizeOne);
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            if (!exMemoryCache.TryGetValue(key, out object result))
            {
                using ICacheEntry entry = exMemoryCache.CreateEntry(key);

                result = valueFactory(key);
                entry.Value = result;
                entry.SetSize(1);
            }

            return (V)result;
        }

        public bool TryGet(K key, out V value)
        {
            throw new NotImplementedException();
        }

        public bool TryRemove(K key)
        {
            throw new NotImplementedException();
        }

        public bool TryUpdate(K key, V value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public class MemoryCacheOptionsAccessor
        : Microsoft.Extensions.Options.IOptions<MemoryCacheOptions>
    {
        private readonly MemoryCacheOptions options = new MemoryCacheOptions();

        public MemoryCacheOptions Value => this.options;

    }
}
