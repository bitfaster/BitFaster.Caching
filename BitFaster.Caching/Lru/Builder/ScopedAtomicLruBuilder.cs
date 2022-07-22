using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;

namespace BitFaster.Caching.Lru.Builder
{
    public class ScopedAtomicLruBuilder<K, V, W> : LruBuilderBase<K, V, ScopedAtomicLruBuilder<K, V, W>, IScopedCache<K, V>> where V : IDisposable where W : IScoped<V>
    {
        private readonly ConcurrentLruBuilder<K, AsyncAtomicFactory<K, W>> inner;

        internal ScopedAtomicLruBuilder(ConcurrentLruBuilder<K, AsyncAtomicFactory<K, W>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override IScopedCache<K, V> Build()
        {
            // TODO: This is actually wrong
            var level1 = inner.Build() as ICache<K, AsyncAtomicFactory<K, Scoped<V>>>;
            var level2 = new AtomicFactoryAsyncCache<K, Scoped<V>>(level1);
            return new ScopedCache<K, V>(level2);
        }
    }
}
