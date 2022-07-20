using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru.Builder
{
    public class ScopedAtomicLruBuilder<K, V, W> : LruBuilderBase<K, V, ScopedAtomicLruBuilder<K, V, W>, IScopedCache<K, V>> where V : IDisposable where W : IScoped<V>
    {
        private readonly ConcurrentLruBuilder<K, AsyncAtomic<K, W>> inner;

        internal ScopedAtomicLruBuilder(ConcurrentLruBuilder<K, AsyncAtomic<K, W>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override IScopedCache<K, V> Build()
        {
            var level1 = inner.Build() as ICache<K, AsyncAtomic<K, Scoped<V>>>;
            var level2 = new AtomicCacheDecorator<K, Scoped<V>>(level1);
            return new ScopedCache<K, V>(level2);
        }
    }
}
