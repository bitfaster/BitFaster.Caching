using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    // simple decorator, compatible with all ICache
    public class ScopedCache<K, V> : IScopedCache<K, V> where V : IDisposable
    {
        private readonly ICache<K, Scoped<V>> cache;

        public ScopedCache(ICache<K, Scoped<V>> cache)
        {
            this.cache = cache;
        }

        public int Capacity => this.cache.Capacity;

        public int Count => this.cache.Count;

        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new Scoped<V>(value));
        }

        public void Clear()
        {
            this.cache.Clear();
        }

        public Lifetime<V> GetOrAdd(K key, Func<K, Scoped<V>> valueFactory)
        {
            var spinwait = new SpinWait();
            while (true)
            {
                var scope = cache.GetOrAdd(key, k => valueFactory(k));

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }

                spinwait.SpinOnce();
            }
        }

        public async Task<Lifetime<V>> GetOrAddAsync(K key, Func<K, Task<Scoped<V>>> valueFactory)
        {
            var spinwait = new SpinWait();
            while (true)
            {
                var scope = await cache.GetOrAddAsync(key, valueFactory);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }

                spinwait.SpinOnce();
            }
        }

        public void Trim(int itemCount)
        {
            this.cache.Trim(itemCount);
        }

        public bool TryGet(K key, out Lifetime<V> lifetime)
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
            return this.cache.TryUpdate(key, new Scoped<V>(value));
        }
    }
}
