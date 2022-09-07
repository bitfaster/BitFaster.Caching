using System;

namespace BitFaster.Caching.Lru.Builder
{
    public sealed class ScopedAsyncConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, ScopedAsyncConcurrentLruBuilder<K, V>, IScopedAsyncCache<K, V>> where V : IDisposable
    {
        private readonly AsyncConcurrentLruBuilder<K, Scoped<V>> inner;

        internal ScopedAsyncConcurrentLruBuilder(AsyncConcurrentLruBuilder<K, Scoped<V>> inner)
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
