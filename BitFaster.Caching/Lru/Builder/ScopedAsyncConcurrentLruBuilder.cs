using System;

namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLru with scoped values.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
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
            info.ThrowIfExpirySpecified("AsScoped");

            // this is a legal type conversion due to the generic constraint on W
            var scopedInnerCache = inner.Build() as IAsyncCache<K, Scoped<V>>;

            return new ScopedAsyncCache<K, V>(scopedInnerCache);
        }
    }
}
