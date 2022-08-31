using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BitFaster.Caching.Lru
{
    [ExcludeFromCodeCoverage]
    internal class LruDebugView<K, V>
    {
        private readonly ICache<K, V> cache;

        public LruDebugView(ICache<K, V> cache)
        {
            if (cache is null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            this.cache = cache;
        }

        public KeyValuePair<K, V>[] Items
        {
            get
            {
                var items = new KeyValuePair<K, V>[cache.Count];

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
