using System;
using System.Collections.Concurrent;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// Convenience methods for using AtomicFactory with ConcurrentDictionary. 
    /// </summary>
    public static class ConcurrentDictionaryExtensions
    {
        public static V GetOrAdd<K, V>(this ConcurrentDictionary<K, AtomicFactory<K, V>> cache, K key, Func<K, V> valueFactory)
        {
            var atomicFactory = cache.GetOrAdd(key, _ => new AtomicFactory<K, V>());
            return atomicFactory.GetValue(key, valueFactory);
        }

        public static V GetOrAdd<K, V, TArg>(this ConcurrentDictionary<K, AtomicFactory<K, V>> cache, K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
        {
            var atomicFactory = cache.GetOrAdd(key, _ => new AtomicFactory<K, V>());
            return atomicFactory.GetValue(key, valueFactory, factoryArgument);
        }

        public static bool TryGetValue<K, V>(this ConcurrentDictionary<K, AtomicFactory<K, V>> cache, K key, out V value)
        {
            AtomicFactory<K, V> output;
            var ret = cache.TryGetValue(key, out output);

            if (ret && output.IsValueCreated)
            {
                value = output.ValueIfCreated;
                return true;
            }

            value = default;
            return false;
        }
    }
}
