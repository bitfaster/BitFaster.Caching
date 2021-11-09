using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lazy
{
    public static class AtomicCacheExtensions
    {
        public static V GetOrAdd<K, V>(this ICache<K, Atomic<K, V>> cache, K key, Func<K, V> valueFactory)
        {
            return cache
                .GetOrAdd(key, _ => new Atomic<K, V>())
                .GetValue(key, valueFactory);
        }

        //public static async Task<V> GetOrAddAsync<K, V>(this ICache<K, Atomic<V>> cache, K key, Func<K, Task<V>> valueFactory)
        //{
        //    var synchronized = cache.GetOrAdd(key, _ => new Atomic<V>());
        //    return synchronized.GetValue(() => valueFactory(key).GetAwaiter().GetResult());
        //}

        public static void AddOrUpdate<K, V>(this ICache<K, Atomic<K, V>> cache, K key, V value)
        {
            cache.AddOrUpdate(key, new Atomic<K, V>(value));
        }

        public static bool TryUpdate<K, V>(this ICache<K, Atomic<K, V>> cache, K key, V value)
        {
            return cache.TryUpdate(key, new Atomic<K, V>(value));
        }

        public static bool TryGet<K, V>(this ICache<K, Atomic<K, V>> cache, K key, out V value)
        {
            Atomic<K, V> output;
            bool ret = cache.TryGet(key, out output);

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
