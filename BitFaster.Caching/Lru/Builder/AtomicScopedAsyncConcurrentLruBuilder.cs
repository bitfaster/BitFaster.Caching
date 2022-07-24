using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;

namespace BitFaster.Caching.Lru.Builder
{
    public sealed class AtomicScopedAsyncConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, AtomicScopedAsyncConcurrentLruBuilder<K, V>, IScopedAsyncCache<K, V>> where V : IDisposable
    {
        private readonly AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>> inner;

        internal AtomicScopedAsyncConcurrentLruBuilder(AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override IScopedAsyncCache<K, V> Build()
        {
            // this is a legal type conversion due to the generic constraint on W
            var scopedInnerCache = inner.Build() as ICache<K, ScopedAsyncAtomicFactory<K, V>>;

            return new AtomicFactoryScopedAsyncCache<K, V>(scopedInnerCache);
        }
    }
}
