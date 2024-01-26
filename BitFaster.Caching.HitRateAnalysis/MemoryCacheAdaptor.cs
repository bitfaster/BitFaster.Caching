﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace BitFaster.Caching.HitRateAnalysis
{
    public class MemoryCacheAdaptor<K, V> : ICache<K, V>
    {
        MemoryCacheOptionsAccessor accessor;
        MemoryCache exMemoryCache;
        CachePolicy policy;
        CacheMetrics metrics;

        public MemoryCacheAdaptor(int capacity)
        {
            accessor = new MemoryCacheOptionsAccessor();
            accessor.Value.SizeLimit = capacity;

            exMemoryCache = new MemoryCache(accessor);
            policy = new CachePolicy(new Optional<IBoundedPolicy>(new BoundedPolicy(capacity)), Optional<ITimePolicy>.None());
            metrics = new CacheMetrics();
        }

        public int Count => throw new NotImplementedException();

        public Optional<ICacheMetrics> Metrics => new Optional<ICacheMetrics>(this.metrics);

        public Optional<ICacheEvents<K, V>> Events => throw new NotImplementedException();

        public CachePolicy Policy => this.policy;

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

                this.metrics.requestMissCount++;
                ThreadPoolInspector.WaitForEmpty();
            }
            else
            {
                this.metrics.requestHitCount++;
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

        private class BoundedPolicy : IBoundedPolicy
        {
            private int capacity;

            public BoundedPolicy(int capacity)
            {
                this.capacity = capacity;
            }

            public int Capacity => this.capacity;

            public void Trim(int itemCount)
            {
                throw new NotImplementedException();
            }
        }

        private class CacheMetrics : ICacheMetrics
        {
            public long requestHitCount;
            public long requestMissCount;

            public double HitRatio => (double)requestHitCount / (double)Total;

            public long Total => requestHitCount + requestMissCount;

            public long Hits => requestHitCount;

            public long Misses => requestMissCount;

            public long Evicted => throw new NotImplementedException();

            public long Updated => throw new NotImplementedException();
        }
    }

    public class MemoryCacheOptionsAccessor
        : Microsoft.Extensions.Options.IOptions<MemoryCacheOptions>
    {
        private readonly MemoryCacheOptions options = new MemoryCacheOptions();

        public MemoryCacheOptions Value => this.options;

    }
}
