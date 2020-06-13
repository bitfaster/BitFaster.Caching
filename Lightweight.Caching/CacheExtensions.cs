using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Lightweight.Caching
{
    public static class CacheExtensions
    {
        public static ScopedDisposable<V>.Lifetime GetOrAddScope<K, V>(
            this ICache<K, ScopedDisposable<V>> cache, 
            K key, 
            Func<K, ScopedDisposable<V>> valueFactory) where V : IDisposable
        {
            return cache.GetOrAdd(key, valueFactory).CreateLifetime();
        }
    }
}
