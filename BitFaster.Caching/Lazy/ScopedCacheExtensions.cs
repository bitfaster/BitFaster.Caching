using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public static class ScopedCacheExtensions
    {
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

        public static Lifetime<T> ScopedGetOrAdd<K, T>(this ICache<K, Scoped<T>> cache, K key, Func<K, T> valueFactory)
            where T : IDisposable
        {
            while (true)
            {
                // Note: allocates a closure on every call
                var scope = cache.GetOrAdd(key, k => new Scoped<T>(valueFactory(k)));

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }

        public static Lifetime<T> ScopedGetOrAddProtected<K, T>(this ICache<K, Scoped<T>> cache, K key, Func<K, Scoped<T>> valueFactory)
            where T : IDisposable
        {
            int c = 0;
            while (true)
            {
                var scope = cache.GetOrAdd(key, k => valueFactory(k));

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }

                if (c++ > 5)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public static async Task<Lifetime<T>> ScopedGetOrAddAsync<K, T>(this ICache<K, Scoped<T>> cache, K key, Func<K, Task<Scoped<T>>> valueFactory)
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
