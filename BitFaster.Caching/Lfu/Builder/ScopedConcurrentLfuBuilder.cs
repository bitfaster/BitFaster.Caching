using System;

namespace BitFaster.Caching.Lfu.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLfu with scoped values.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    /// <typeparam name="W">The type of the wrapped cache value.</typeparam>
    public sealed class ScopedConcurrentLfuBuilder<K, V, W> : LfuBuilderBase<K, V, ScopedConcurrentLfuBuilder<K, V, W>, IScopedCache<K, V>>
        where K : notnull
        where V : IDisposable
        where W : IScoped<V>
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
            info.ThrowIfExpirySpecified("AsScoped");

            // this is a legal type conversion due to the generic constraint on W
            var scopedInnerCache = inner.Build() as ICache<K, Scoped<V>>;

            return new ScopedCache<K, V>(scopedInnerCache!);
        }
    }
}
