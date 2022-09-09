
namespace BitFaster.Caching.Lfu.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLfu as IAsyncCache.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    public sealed class AsyncConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, AsyncConcurrentLfuBuilder<K, V>, IAsyncCache<K, V>>
    {
        internal AsyncConcurrentLfuBuilder(LfuInfo<K> info)
            : base(info)
        {
        }

        ///<inheritdoc/>
        public override IAsyncCache<K, V> Build()
        {
            return new ConcurrentLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, info.BufferConfiguration);
        }
    }
}
