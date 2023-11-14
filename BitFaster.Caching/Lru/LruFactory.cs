using BitFaster.Caching.Lru.Builder;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Factory class for creating ConcurrentLru variants.
    /// </summary>
    internal static class LruFactory<K, V>
    {
        /// <summary>
        /// Creates a ConcurrentLru instance based on the provided LruInfo.
        /// </summary>
        /// <param name="info">The LruInfo</param>
        /// <returns>A ConcurrentLru</returns>
        internal static ICache<K, V> CreateConcurrent(LruInfo<K> info)
        {
            if (info.TimeToExpireAfterWrite.HasValue && info.TimeToExpireAfterAccess.HasValue)
                Throw.InvalidOp("Specifying both ExpireAfterWrite and ExpireAfterAccess is not supported.");

            var expiry = info.GetExpiry<V>();

            if (info.TimeToExpireAfterWrite.HasValue && expiry != null)
                Throw.InvalidOp("Specifying both ExpireAfterWrite and ExpireAfter is not supported.");

            if (info.TimeToExpireAfterAccess.HasValue && expiry != null)
                Throw.InvalidOp("Specifying both ExpireAfterAccess and ExpireAfter is not supported.");

            return (info.WithMetrics, info.TimeToExpireAfterWrite.HasValue, info.TimeToExpireAfterAccess.HasValue, expiry == null) switch
            {
                (true, false, false, false) => new ConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
                (true, true, false, false) => new ConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                (false, true, false, false) => new FastConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                (true, false, true, false) => CreateExpireAfterAccess<TelemetryPolicy<K, V>>(info),
                (false, false, true, false) => CreateExpireAfterAccess<NoTelemetryPolicy<K, V>>(info),
                (true, false, false, true) => CreateExpireAfter<TelemetryPolicy<K, V>>(info, expiry),
                (false, false, false, true) => CreateExpireAfter<NoTelemetryPolicy<K, V>>(info, expiry),
                _ => new FastConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
            };
        }

        private static ICache<K, V> CreateExpireAfterAccess<TP>(LruInfo<K> info) where TP : struct, ITelemetryPolicy<K, V>
        {
            return new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, AfterAccessLongTicksPolicy<K, V>, TP>(
                info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new AfterAccessLongTicksPolicy<K, V>(info.TimeToExpireAfterAccess.Value), default);
        }

        private static ICache<K, V> CreateExpireAfter<TP>(LruInfo<K> info, IExpiryCalculator<K, V> expiry) where TP : struct, ITelemetryPolicy<K, V>
        {
            return new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, DiscretePolicy<K, V>, TP>(
                    info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new DiscretePolicy<K, V>(expiry), default);
        }
    }
}
