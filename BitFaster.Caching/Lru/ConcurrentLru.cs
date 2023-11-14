using BitFaster.Caching.Lru.Builder;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Factory class for creating ConcurrentLru variants.
    /// </summary>
    internal static class ConcurrentLru
    {
        /// <summary>
        /// Creates a ConcurrentLru instance based on the provided LruInfo.
        /// </summary>
        /// <param name="info">The LruInfo</param>
        /// <returns>A ConcurrentLru</returns>
        internal static ICache<K, V> Create<K, V>(LruInfo<K> info)
        {
            if (info.TimeToExpireAfterWrite.HasValue && info.TimeToExpireAfterAccess.HasValue)
                Throw.InvalidOp("Specifying both ExpireAfterWrite and ExpireAfterAccess is not supported.");

            return (info.WithMetrics, info.TimeToExpireAfterWrite.HasValue, info.TimeToExpireAfterAccess.HasValue) switch
            {
                (true, false, false) => new ConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
                (true, true, false) => new ConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                (false, true, false) => new FastConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                (true, false, true) => CreateExpireAfterAccess<K, V, TelemetryPolicy<K, V>>(info),
                (false, false, true) => CreateExpireAfterAccess<K, V, NoTelemetryPolicy<K, V>>(info),
                _ => new FastConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
            };
        }

        private static ICache<K, V> CreateExpireAfterAccess<K, V, TP>(LruInfo<K> info) where TP : struct, ITelemetryPolicy<K, V>
        {
            return new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, AfterAccessLongTicksPolicy<K, V>, TP>(
                info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new AfterAccessLongTicksPolicy<K, V>(info.TimeToExpireAfterAccess.Value), default);
        }
    }
}
