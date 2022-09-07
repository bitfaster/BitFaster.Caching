using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLru as ICache with atomic value creation.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    public class AtomicConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, AtomicConcurrentLruBuilder<K, V>, ICache<K, V>>
    {
        private readonly ConcurrentLruBuilder<K, AtomicFactory<K, V>> inner;

        internal AtomicConcurrentLruBuilder(ConcurrentLruBuilder<K, AtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override ICache<K, V> Build()
        {
            var level1 = inner.Build();
            return new AtomicFactoryCache<K, V>(level1);
        }
    }
}
