using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public static class ScopedAsyncAtomicExtensions
    { 
        public static async Task<AsyncAtomicLifetime<K, V>> GetOrAddAsync<K, V>(this ICache<K, ScopedAsyncAtomic<K, V>> cache, K key, Func<K, Task<V>> valueFactory) where V : IDisposable
        {
            while (true)
            {
                var scope = cache.GetOrAdd(key, _ => new ScopedAsyncAtomic<K, V>());
                var result = await scope.TryCreateLifetimeAsync(key, valueFactory).ConfigureAwait(false);

                if (result.succeeded)
                {
                    return result.lifetime;
                }
            }
        }

        public static void AddOrUpdate<K, V>(this ICache<K, ScopedAsyncAtomic<K, V>> cache, K key, V value) where V : IDisposable
        {
            cache.AddOrUpdate(key, new ScopedAsyncAtomic<K, V>(value));
        }

        public static bool TryUpdate<K, V>(this ICache<K, ScopedAsyncAtomic<K, V>> cache, K key, V value) where V : IDisposable
        {
            return cache.TryUpdate(key, new ScopedAsyncAtomic<K, V>(value));
        }

        // TODO: TryGetLifetime?
        public static bool TryGetLifetime<K, V>(this ICache<K, ScopedAsyncAtomic<K, V>> cache, K key, out AsyncAtomicLifetime<K, V> value) where V : IDisposable
        {
            if (cache.TryGet(key, out var scoped))
            {
                if (scoped.TryCreateLifetime(out var lifetime))
                {
                    value = lifetime;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
