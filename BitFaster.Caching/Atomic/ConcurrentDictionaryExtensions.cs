using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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

        public static bool TryRemove<K, V>(this ConcurrentDictionary<K, AtomicFactory<K, V>> cache, KeyValuePair<K, V> item)
        {
            var kvp = new KeyValuePair<K, AtomicFactory<K, V>>(item.Key, new AtomicFactory<K, V>(item.Value));
#if NET6_0_OR_GREATER
            return cache.TryRemove(kvp);
#else
            // https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
            return ((ICollection<KeyValuePair<K, AtomicFactory<K, V>>>)cache).Remove(kvp);
#endif
        }

        public static bool TryRemove<K, V>(this ConcurrentDictionary<K, AtomicFactory<K, V>> cache, K key, out V value)
        {
            if (cache.TryRemove(key, out var atomic))
            {
                value = atomic.ValueIfCreated;
                return true;
            }

            value = default;
            return false;
        }
    }
}
