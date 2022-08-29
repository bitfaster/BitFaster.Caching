using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lfu.Builder
{
    public sealed class AtomicScopedAsyncConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, AtomicScopedAsyncConcurrentLfuBuilder<K, V>, IScopedAsyncCache<K, V>> where V : IDisposable
    {
        private readonly AsyncConcurrentLfuBuilder<K, ScopedAsyncAtomicFactory<K, V>> inner;

        internal AtomicScopedAsyncConcurrentLfuBuilder(AsyncConcurrentLfuBuilder<K, ScopedAsyncAtomicFactory<K, V>> inner)
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
