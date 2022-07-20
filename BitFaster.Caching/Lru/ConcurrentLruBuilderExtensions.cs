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
        public static ScopedLruBuilder<K, V, Scoped<V>> WithScopedValues<K, V>(this ConcurrentLruBuilder<K, V> b) where V : IDisposable
        {
            var scoped = new ConcurrentLruBuilder<K, Scoped<V>>(b.info);
            return new ScopedLruBuilder<K, V, Scoped<V>>(scoped);
        }
    }
}
