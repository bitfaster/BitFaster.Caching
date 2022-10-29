using System;
using System.Collections.Generic;

namespace BitFaster.Caching
{
    internal class ScopedCacheDebugView<K, V> where V : IDisposable
    {
        private readonly IScopedCache<K, V> cache;

        public ScopedCacheDebugView(IScopedCache<K, V> cache)
        {
            if (cache is null)
            {
                Ex.ThrowArgNull(ExceptionArgument.cache);
            }

            this.cache = cache;
        }

        public KeyValuePair<K, Scoped<V>>[] Items
        {
            get
            {
                var items = new KeyValuePair<K, Scoped<V>>[cache.Count];

                var index = 0;
                foreach (var kvp in cache)
                {
                    items[index++] = kvp;
                }
                return items;
            }
        }

        public ICacheMetrics Metrics => cache.Metrics.Value;
    }
}
