﻿
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BitFaster.Caching
{
    [ExcludeFromCodeCoverage]
    internal class CacheDebugView<K, V>
        where K : notnull
    {
        private readonly ICache<K, V> cache;

        public CacheDebugView(ICache<K, V> cache)
        {
            if (cache is null)
                Throw.ArgNull(ExceptionArgument.cache);

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

        public ICacheMetrics? Metrics => cache.Metrics.Value;
    }
}
