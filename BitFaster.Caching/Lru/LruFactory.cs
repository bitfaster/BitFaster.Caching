using System.Linq.Expressions;
using BitFaster.Caching.Lru.Builder;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Factory class for creating ConcurrentLru variants.
    /// </summary>
    public static class LruFactory<K, V>
    {
        /// <summary>
        /// Creates a ConcurrentLru instance based on the provided LruInfo.
        /// </summary>
        /// <param name="info">The LruInfo</param>
        /// <returns>A ConcurrentLru</returns>
        public static ICache<K, V> CreateConcurrent(LruInfo<K> info)
        {
            if (info.TimeToExpireAfterWrite.HasValue && info.TimeToExpireAfterAccess.HasValue)
                Throw.InvalidOp("Specifying both ExpireAfterWrite and ExpireAfterAccess is not supported.");

            //return info switch
            //{
            //    LruInfo<K> i when i.WithMetrics && !i.TimeToExpireAfterWrite.HasValue && !i.TimeToExpireAfterAccess.HasValue => new ConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
            //    LruInfo<K> i when i.WithMetrics && i.TimeToExpireAfterWrite.HasValue && !i.TimeToExpireAfterAccess.HasValue => new ConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
            //    LruInfo<K> i when i.TimeToExpireAfterWrite.HasValue && !i.TimeToExpireAfterAccess.HasValue => new FastConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
            //    LruInfo<K> i when i.WithMetrics && !i.TimeToExpireAfterWrite.HasValue && i.TimeToExpireAfterAccess.HasValue => CreateExpireAfterAccess<TelemetryPolicy<K, V>>(info),
            //    LruInfo<K> i when !i.TimeToExpireAfterWrite.HasValue && i.TimeToExpireAfterAccess.HasValue => CreateExpireAfterAccess<NoTelemetryPolicy<K, V>>(info),
            //    _ => new FastConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
            //};

            return (info.WithMetrics, info.TimeToExpireAfterWrite.HasValue, info.TimeToExpireAfterAccess.HasValue) switch
            {
                (true, false, false) => new ConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
                (true, true, false) => new ConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                (false, true, false) => new FastConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                (true, false, true) => CreateExpireAfterAccess<TelemetryPolicy<K, V>>(info),
                (false, false, true) => CreateExpireAfterAccess<NoTelemetryPolicy<K, V>>(info),
                _ => new FastConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
            };
        }

        /// <summary>
        /// Creates a ConcurrentLru instance based on the provided LruInfo.
        /// </summary>
        /// <param name="info">The LruInfo</param>
        /// <returns>A ConcurrentLru</returns>
        public static IAsyncCache<K, V> CreateAsyncConcurrent(LruInfo<K> info)
        {
            return CreateConcurrent(info) as IAsyncCache<K, V>;
        }

        private static ICache<K, V> CreateExpireAfterAccess<TP>(LruInfo<K> info) where TP : struct, ITelemetryPolicy<K, V>
        {
            return new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, AfterAccessLongTicksPolicy<K, V>, TP>(
                info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new AfterAccessLongTicksPolicy<K, V>(info.TimeToExpireAfterAccess.Value), default);
        }
    }
}
