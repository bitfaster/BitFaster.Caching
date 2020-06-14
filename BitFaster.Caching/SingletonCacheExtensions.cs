using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching
{
    public static class SingletonCacheExtensions
    {
        public static SingletonCache<TKey, TValue>.Handle Acquire<TKey, TValue>(this SingletonCache<TKey, TValue> cache, TKey key) 
            where TValue : new()
        {
            return cache.Acquire(key, _ => new TValue());
        }
    }
}
