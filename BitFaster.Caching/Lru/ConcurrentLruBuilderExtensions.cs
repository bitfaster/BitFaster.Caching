using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru.Builder;

namespace BitFaster.Caching.Lru
{
    public static class ConcurrentLruBuilderExtensions
    {
        /// <summary>
        /// Wrap IDisposable values in a lifetime scope. Scoped caches return lifetimes that prevent
        /// values from being disposed until the calling code completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>A ScopedLruBuilder</returns>
        public static ScopedLruBuilder<K, V, Scoped<V>> WithScopedValues<K, V>(this ConcurrentLruBuilder<K, V> builder) where V : IDisposable
        {
            var scoped = new ConcurrentLruBuilder<K, Scoped<V>>(builder.info);
            return new ScopedLruBuilder<K, V, Scoped<V>>(scoped);
        }
    }
}
