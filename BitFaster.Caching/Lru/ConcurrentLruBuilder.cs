
using BitFaster.Caching.Lru.Builder;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// A builder of ICache and IScopedCache instances with the following configuration
    /// settings:
    /// <list type="bullet">
    ///   <item><description>The maximum size.</description></item>
    ///   <item><description>The concurrency level.</description></item>
    ///   <item><description>The key comparer.</description></item>
    /// </list>
    /// The following features can be selected which change the underlying cache implementation: 
    /// <list type="bullet">
    ///   <item><description>Collect metrics (e.g. hit rate). Small perf penalty.</description></item>
    ///   <item><description>Time based expiration, measured since last write.</description></item>
    ///   <item><description>Time based expiration, measured since last read.</description></item>
    ///   <item><description>Scoped IDisposable values.</description></item>
    ///   <item><description>Atomic value factory.</description></item>
    /// </list>
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    public sealed class ConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, ConcurrentLruBuilder<K, V>, ICache<K, V>>
    {
        /// <summary>
        /// Creates a ConcurrentLruBuilder. Chain method calls onto ConcurrentLruBuilder to configure the cache then call Build to create a cache instance.
        /// </summary>
        public ConcurrentLruBuilder()
            : base(new LruInfo<K>())
        {
        }

        internal ConcurrentLruBuilder(LruInfo<K> info)
            : base(info)
        {
        }

        ///<inheritdoc/>
        public override ICache<K, V> Build()
        {
            return info switch
            {
                LruInfo<K> i when i.WithMetrics && !i.TimeToExpireAfterWrite.HasValue && !i.TimeToExpireAfterRead.HasValue => new ConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
                LruInfo<K> i when i.WithMetrics && i.TimeToExpireAfterWrite.HasValue && !i.TimeToExpireAfterRead.HasValue => new ConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                LruInfo<K> i when i.TimeToExpireAfterWrite.HasValue && !i.TimeToExpireAfterRead.HasValue => new FastConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.TimeToExpireAfterWrite.Value),
                LruInfo<K> i when i.WithMetrics && !i.TimeToExpireAfterWrite.HasValue && i.TimeToExpireAfterRead.HasValue =>
                    new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, AfterReadLongTicksPolicy<K, V>, TelemetryPolicy<K, V>>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new AfterReadLongTicksPolicy<K,V>(info.TimeToExpireAfterRead.Value), default),
                LruInfo<K> i when !i.TimeToExpireAfterWrite.HasValue && i.TimeToExpireAfterRead.HasValue =>
                    new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, AfterReadLongTicksPolicy<K, V>, NoTelemetryPolicy<K, V>>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new AfterReadLongTicksPolicy<K,V>(info.TimeToExpireAfterRead.Value), default),
                LruInfo<K> i when i.WithMetrics && i.TimeToExpireAfterWrite.HasValue && i.TimeToExpireAfterRead.HasValue =>
                    new ConcurrentLruCore<K, V, LongTickCountReadWriteLruItem<K, V>, AfterReadWriteLongTicksPolicy<K, V>, TelemetryPolicy<K, V>>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new AfterReadWriteLongTicksPolicy<K, V>(info.TimeToExpireAfterRead.Value, info.TimeToExpireAfterWrite.Value), default),
                LruInfo<K> i when i.TimeToExpireAfterWrite.HasValue && i.TimeToExpireAfterRead.HasValue =>
                    new ConcurrentLruCore<K, V, LongTickCountReadWriteLruItem<K, V>, AfterReadWriteLongTicksPolicy<K, V>, NoTelemetryPolicy<K, V>>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new AfterReadWriteLongTicksPolicy<K, V>(info.TimeToExpireAfterRead.Value, info.TimeToExpireAfterWrite.Value), default),
                _ => new FastConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer),
            };
        }
    }
}
