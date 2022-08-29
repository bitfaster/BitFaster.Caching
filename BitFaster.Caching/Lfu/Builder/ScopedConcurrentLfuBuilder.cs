using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lfu.Builder
{
    public sealed class ScopedConcurrentLfuBuilder<K, V, W> : LfuBuilderBase<K, V, ScopedConcurrentLfuBuilder<K, V, W>, IScopedCache<K, V>> where V : IDisposable where W : IScoped<V>
    {
        private readonly ConcurrentLfuBuilder<K, W> inner;

        internal ScopedConcurrentLfuBuilder(ConcurrentLfuBuilder<K, W> inner)
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
