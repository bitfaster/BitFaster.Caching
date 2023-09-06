using System;
using System.Collections.Concurrent;

namespace BitFaster.Caching.Atomic
{
    public static class ConcurrentDictionaryExtensions
    {
        public static V GetOrAdd<K, V>(this ConcurrentDictionary<K, AtomicFactory<K, V>> cache, K key, Func<K, V> valueFactory)
        {
            var atomicFactory = cache.GetOrAdd(key, _ => new AtomicFactory<K, V>());
            return atomicFactory.GetValue(key, valueFactory);
        }
    }
}
