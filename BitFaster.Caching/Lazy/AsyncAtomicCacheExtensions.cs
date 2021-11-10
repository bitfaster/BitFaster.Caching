using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    public static class AsyncAtomicCacheExtensions
    {
        //public static V GetOrAdd<K, V>(this ICache<K, AsyncAtomic<V>> cache, K key, Func<K, V> valueFactory)
        //{
        //    return cache
        //        .GetOrAdd(key, k => new AsyncAtomic<V>())
        //        .GetValueAsync(() => Task.FromResult(valueFactory(key)))
        //        .GetAwaiter().GetResult();
        //}

        public static Task<V> GetOrAddAsync<K, V>(this ICache<K, AsyncAtomic<K, V>> cache, K key, Func<K, Task<V>> valueFactory)
        {
            var synchronized = cache.GetOrAdd(key, _ => new AsyncAtomic<K, V>());
            return synchronized.GetValueAsync(key, valueFactory);
        }

        public static void AddOrUpdate<K, V>(this ICache<K, AsyncAtomic<K, V>> cache, K key, V value)
        {
            cache.AddOrUpdate(key, new AsyncAtomic<K, V>(value));
        }

        public static bool TryUpdate<K, V>(this ICache<K, AsyncAtomic<K, V>> cache, K key, V value)
        {
            return cache.TryUpdate(key, new AsyncAtomic<K, V>(value));
        }

        public static bool TryGet<K, V>(this ICache<K, AsyncAtomic<K, V>> cache, K key, out V value)
        {
            AsyncAtomic<K, V> output;
            bool ret = cache.TryGet(key, out output);

            // TOOD: should this return false if the value is not created but the key exists?
            // that would indicate a race between GetOrAdd and TryGet, maybe it should return false?
            if (ret)
            {
                value = output.ValueIfCreated;
            }
            else
            {
                value = default;
            }

            return ret;
        }
    }
}
