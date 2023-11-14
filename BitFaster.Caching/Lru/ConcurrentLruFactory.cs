using BitFaster.Caching.Lru.Builder;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Factory class for creating ConcurrentLru variants.
    /// </summary>
    public static class ConcurrentLruFactory<K, V>
    {
        /// <summary>
        /// Creates a ConcurrentLru instance based on the provided LruInfo.
        /// </summary>
        /// <param name="info">The LruInfo</param>
        /// <returns>A ConcurrentLru</returns>
        public static ICache<K, V> CreateCache(LruInfo<K> info)
        {
            if (info.TimeToExpireAfterWrite.HasValue && info.TimeToExpireAfterAccess.HasValue)
                Throw.InvalidOp("Specifying both ExpireAfterWrite and ExpireAfterAccess is not supported.");

            return info switch
            {
                LruInfo<K> i when i.WithMetrics && !i.TimeToExpireAfterWrite.HasValue && !i.TimeToExpireAfterAccess.HasValue => new ConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
                LruInfo<K> i when i.WithMetrics && i.TimeToExpireAfterWrite.HasValue && !i.TimeToExpireAfterAccess.HasValue => new ConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                LruInfo<K> i when i.TimeToExpireAfterWrite.HasValue && !i.TimeToExpireAfterAccess.HasValue => new FastConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                LruInfo<K> i when i.WithMetrics && !i.TimeToExpireAfterWrite.HasValue && i.TimeToExpireAfterAccess.HasValue => CreateExpireAfterAccess<TelemetryPolicy<K, V>>(info),
                LruInfo<K> i when !i.TimeToExpireAfterWrite.HasValue && i.TimeToExpireAfterAccess.HasValue => CreateExpireAfterAccess<NoTelemetryPolicy<K, V>>(info),
                _ => new FastConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
            };
        }

        /// <summary>
        /// Creates a ConcurrentLru instance based on the provided LruInfo.
        /// </summary>
        /// <param name="info">The LruInfo</param>
        /// <returns>A ConcurrentLru</returns>
        public static IAsyncCache<K, V> CreateAsyncCache(LruInfo<K> info)
        {
            return CreateCache(info) as IAsyncCache<K, V>;
        }

        private static ICache<K, V> CreateExpireAfterAccess<TP>(LruInfo<K> info) where TP : struct, ITelemetryPolicy<K, V>
        {
            return new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, AfterAccessLongTicksPolicy<K, V>, TP>(
                info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new AfterAccessLongTicksPolicy<K, V>(info.TimeToExpireAfterAccess.Value), default);
        }
    }
}
