using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lru.Builder
{
    public sealed class ScopedConcurrentLruBuilder<K, V, W> : LruBuilderBase<K, V, ScopedConcurrentLruBuilder<K, V, W>, IScopedCache<K, V>> where V : IDisposable where W : IScoped<V>
    {
        private readonly ConcurrentLruBuilder<K, W> inner;

        internal ScopedConcurrentLruBuilder(ConcurrentLruBuilder<K, W> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override IScopedCache<K, V> Build()
        {
            // this is a legal type conversion due to the generic constraint on W
            var scopedInnerCache = inner.Build() as ICache<K, Scoped<V>>;

            return new ScopedCache<K, V>(scopedInnerCache);
        }
    }
}
