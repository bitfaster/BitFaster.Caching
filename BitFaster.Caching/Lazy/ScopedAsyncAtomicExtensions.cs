using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
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
    }
}
