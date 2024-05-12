
namespace BitFaster.Caching.Lfu.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLfu as IAsyncCache.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    public sealed class AsyncConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, AsyncConcurrentLfuBuilder<K, V>, IAsyncCache<K, V>>
         where K : notnull
    {
        internal AsyncConcurrentLfuBuilder(LfuInfo<K> info)
            : base(info)
        {
        }

        /// <summary>
        /// Evict after a duration calculated for each item using the specified IExpiryCalculator.
        /// </summary>
        /// <param name="expiry">The expiry calculator that determines item time to expire.</param>
        /// <returns>A ConcurrentLruBuilder</returns>
        public AsyncConcurrentLfuBuilder<K, V> WithExpireAfter(IExpiryCalculator<K, V> expiry)
        {
            this.info.SetExpiry(expiry);
            return this;
        }

        ///<inheritdoc/>
        public override IAsyncCache<K, V> Build()
        {
            return(ConcurrentLfuFactory.Create<K, V>(this.info) as IAsyncCache<K, V>)!;
        }
    }
}
