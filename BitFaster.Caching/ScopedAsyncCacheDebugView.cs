using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BitFaster.Caching
{
    [ExcludeFromCodeCoverage]
    internal class ScopedAsyncCacheDebugView<K, V> where V : IDisposable
    {
        private readonly IScopedAsyncCache<K, V> cache;

        public ScopedAsyncCacheDebugView(IScopedAsyncCache<K, V> cache)
        {
            if (cache is null)
                Throw.ArgNull(ExceptionArgument.cache);

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
