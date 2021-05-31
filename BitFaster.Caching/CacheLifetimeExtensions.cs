using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public static class CacheLifetimeExtensions
    {
        public static Lifetime<T> ScopedGetOrAdd<K, S, T>(this ICache<K, S> cache, K key, Func<K, S> valueFactory)
            where S : Scoped<T>
            where T : IDisposable 
        { 
            while (true)
            {
                var scope = cache.GetOrAdd(key, valueFactory);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }
        public static async Task<Lifetime<T>> ScopedGetOrAdd<K, S, T>(this ICache<K, S> cache, K key, Func<K, Task<S>> valueFactory)
            where S : Scoped<T>
            where T : IDisposable
        {
            while (true)
            {
                var scope = await cache.GetOrAddAsync(key, valueFactory);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }
    }
}
