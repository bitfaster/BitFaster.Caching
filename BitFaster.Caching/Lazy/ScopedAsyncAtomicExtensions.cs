using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    public static class ScopedAsyncAtomicExtensions
    { 
        public static Task<AsyncAtomicLifetime<K, V>> GetOrAddAsync<K, V>(this ICache<K, ScopedAsyncAtomic<K, V>> cache, K key, Func<K, Task<V>> valueFactory) where V : IDisposable
        {
            //return cache.GetOrAdd(key, _ => new ScopedAsyncAtomic<K, V>())
            //    .CreateLifetimeAsync(key, valueFactory);

            while (true)
            {
                var scope = cache.GetOrAdd(key, _ => new ScopedAsyncAtomic<K, V>());

                // TODO: try create lifetime async
                var t = scope.CreateLifetimeAsync(key, valueFactory);

                return t;
            }
        } 
    }
}
