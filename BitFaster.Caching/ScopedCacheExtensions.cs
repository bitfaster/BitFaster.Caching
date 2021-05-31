using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public static class ScopedCacheExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="S"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="cache"></param>
        /// <param name="key"></param>
        /// <param name="valueFactory"></param>
        /// <returns></returns>
        public static Lifetime<T> ScopedGetOrAdd<K, T>(this ICache<K, Scoped<T>> cache, K key, Func<K, Scoped<T>> valueFactory)
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

        public static async Task<Lifetime<T>> ScopedGetOrAdd<K, T>(this ICache<K, Scoped<T>> cache, K key, Func<K, Task<Scoped<T>>> valueFactory)
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
