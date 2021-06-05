using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    // what happens if we completely encapsulate scoped?
    // we can't implement ICache, since return types will now be Lifetime, not T
    public class ScopedCache<K, T> : IScopedCache<K, T> where T : IDisposable
    {
        private readonly ICache<K, Scoped<T>> innerCache;

        public ScopedCache(ICache<K, Scoped<T>> innerCache)
        {
            this.innerCache = innerCache;
        }

        public bool TryGet(K key, out Lifetime<T> value)
        {
            if (innerCache.TryGet(key, out var scope))
            {
                if (scope.TryCreateLifetime(out var lifetime))
                {
                    value = lifetime;
                    return true;
                }
            }

            value = default(Lifetime<T>);
            return false;
        }

        // If we completely encapsulate Scoped, then we must allocate a new value factory for every GetOrAdd call
        public Lifetime<T> GetOrAdd(K key, Func<K, Scoped<T>> valueFactory)
        {
            while (true)
            {
                var scope = innerCache.GetOrAdd(key, valueFactory);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }

        public async Task<Lifetime<T>> GetOrAddAsync(K key, Func<K, Task<Scoped<T>>> valueFactory)
        {
            while (true)
            {
                var scope = await innerCache.GetOrAddAsync(key, valueFactory);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }

        public bool TryRemove(K key)
        {
            return this.innerCache.TryRemove(key);
        }

        public bool TryUpdate(K key, T value)
        { 
            // scoped finializer does not call dispose, so if this fails, discarded new Scoped will not dispose value.
            return this.innerCache.TryUpdate(key, new Scoped<T>(value));
        }

        public void AddOrUpdate(K key, T value)
        { 
            this.innerCache.AddOrUpdate(key, new Scoped<T>(value));
        }
    }
}
