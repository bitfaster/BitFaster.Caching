using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lfu.Builder
{
    public sealed class ScopedAsyncConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, ScopedAsyncConcurrentLfuBuilder<K, V>, IScopedAsyncCache<K, V>> where V : IDisposable
    {
        private readonly AsyncConcurrentLfuBuilder<K, Scoped<V>> inner;

        internal ScopedAsyncConcurrentLfuBuilder(AsyncConcurrentLfuBuilder<K, Scoped<V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override IScopedAsyncCache<K, V> Build()
        {
            // this is a legal type conversion due to the generic constraint on W
            var scopedInnerCache = inner.Build() as IAsyncCache<K, Scoped<V>>;

            return new ScopedAsyncCache<K, V>(scopedInnerCache);
        }
    }
}
