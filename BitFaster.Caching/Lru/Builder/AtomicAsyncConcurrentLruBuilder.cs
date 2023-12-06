using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLru as IAsyncCache with atomic value creation.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    public class AtomicAsyncConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, AtomicAsyncConcurrentLruBuilder<K, V>, IAsyncCache<K, V>>
         where K : notnull
    {
        private readonly ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>> inner;

        internal AtomicAsyncConcurrentLruBuilder(ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override IAsyncCache<K, V> Build()
        {
            info.ThrowIfExpirySpecified("AsAtomic");

            var level1 = inner.Build();
            return new AtomicFactoryAsyncCache<K, V>(level1);
        }
    }
}
