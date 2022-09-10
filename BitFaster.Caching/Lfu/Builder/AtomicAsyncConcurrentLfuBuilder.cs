
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lfu.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLfu as IAsyncCache with atomic value creation.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    public sealed class AtomicAsyncConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, AtomicAsyncConcurrentLfuBuilder<K, V>, IAsyncCache<K, V>>
    {
        private readonly ConcurrentLfuBuilder<K, AsyncAtomicFactory<K, V>> inner;

        internal AtomicAsyncConcurrentLfuBuilder(ConcurrentLfuBuilder<K, AsyncAtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override IAsyncCache<K, V> Build()
        {
            var level1 = inner.Build();
            return new AtomicFactoryAsyncCache<K, V>(level1);
        }
    }
}
