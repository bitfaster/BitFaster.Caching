
namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLru.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    public sealed class AsyncConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, AsyncConcurrentLruBuilder<K, V>, IAsyncCache<K, V>>
    {
        internal AsyncConcurrentLruBuilder(LruInfo<K> info)
            : base(info)
        {
        }

        /// <summary>
        /// Evict after a variable duration specified by an IExpiry instance.
        /// </summary>
        /// <param name="expiry">The expiry that determines item time to live.</param>
        /// <returns>A AsyncConcurrentLruBuilder</returns>
        public AsyncConcurrentLruBuilder<K, V> WithExpiry(IExpiry<K, V> expiry)
        {
            this.info.Expiry = expiry;
            return this;
        }

        ///<inheritdoc/>
        public override IAsyncCache<K, V> Build()
        {
            return info switch
            {
                LruInfo<K> i when i.WithMetrics && !i.TimeToExpireAfterWrite.HasValue => new ConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
                LruInfo<K> i when i.WithMetrics && i.TimeToExpireAfterWrite.HasValue => new ConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                LruInfo<K> i when i.TimeToExpireAfterWrite.HasValue => new FastConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                _ => new FastConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
            };
        }
    }
}
