using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Lightweight.Caching
{
    public static class CacheExtensions
    {
        public static Scoped<V>.Lifetime CreateLifetime<K, V>(
            this ICache<K, Scoped<V>> cache, 
            K key, 
            Func<K, Scoped<V>> valueFactory) where V : IDisposable
        {
            // Should this retry?
            try
            {
                return cache.GetOrAdd(key, valueFactory).CreateLifetime();
            }
            catch (ObjectDisposedException)
            {
                // Retry once - race is unlikely
                return cache.GetOrAdd(key, valueFactory).CreateLifetime();
            }
        }
    }
}
