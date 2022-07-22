using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Synchronized
{
    public class AtomicFactoryScopedAsyncCache<K, V> : IScopedCache<K, V> where V : IDisposable
    {
        public int Capacity => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public ICacheMetrics Metrics => throw new NotImplementedException();

        public ICacheEvents<K, Scoped<V>> Events => throw new NotImplementedException();

        public void AddOrUpdate(K key, V value)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public Lifetime<V> ScopedGetOrAdd(K key, Func<K, Scoped<V>> valueFactory)
        {
            throw new NotImplementedException();
        }

        public Task<Lifetime<V>> ScopedGetOrAddAsync(K key, Func<K, Task<Scoped<V>>> valueFactory)
        {
            throw new NotImplementedException();
        }

        public bool ScopedTryGet(K key, out Lifetime<V> lifetime)
        {
            throw new NotImplementedException();
        }

        public void Trim(int itemCount)
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
    }
}
