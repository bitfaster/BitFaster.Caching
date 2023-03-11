
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
    ///   <item><description>Time based expiration, measured since write.</description></item>
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
            switch (info)
            {
#if NETCOREAPP3_0_OR_GREATER
                case LruInfo<K> i when i.WithMetrics && i.TimeToExpireAfterWrite.HasValue && i.WithHighResolutionTime:
                    return new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, TlruStopwatchPolicy<K, V>, TelemetryPolicy<K, V>>(
                        info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new TlruStopwatchPolicy<K, V>(info.TimeToExpireAfterWrite.Value), default);
                case LruInfo<K> i when !i.WithMetrics && i.TimeToExpireAfterWrite.HasValue && i.WithHighResolutionTime:
                    return new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, TlruStopwatchPolicy<K, V>, NoTelemetryPolicy<K, V>>(
                        info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new TlruStopwatchPolicy<K, V>(info.TimeToExpireAfterWrite.Value), default);
#endif
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
