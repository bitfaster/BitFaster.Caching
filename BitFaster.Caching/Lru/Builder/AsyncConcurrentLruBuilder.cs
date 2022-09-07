
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

        ///<inheritdoc/>
        public override IAsyncCache<K, V> Build()
        {
            switch (info)
            {
                case LruInfo<K> i when i.WithMetrics && !i.TimeToExpireAfterWrite.HasValue:
                    return new ConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer);
                case LruInfo<K> i when i.WithMetrics && i.TimeToExpireAfterWrite.HasValue:
                    return new ConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value);
                case LruInfo<K> i when i.TimeToExpireAfterWrite.HasValue:
                    return new FastConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value);
                default:
                    return new FastConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer);
            }
        }
    }
}
