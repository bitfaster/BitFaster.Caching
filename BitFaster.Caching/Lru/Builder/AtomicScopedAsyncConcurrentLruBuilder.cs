using System;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLru with scoped values.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
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
