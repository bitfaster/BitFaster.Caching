
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

        private static ICache<K, V> CreateExpireAfterAccess<TP>(LruInfo<K> info) where TP : struct, ITelemetryPolicy<K, V>
        {
            return new ConcurrentLruCore<K, V, LongTickCountLruItem<K, V>, AfterAccessLongTicksPolicy<K, V>, TP>(
                info.ConcurrencyLevel, info.Capacity, info.KeyComparer, new AfterAccessLongTicksPolicy<K, V>(info.TimeToExpireAfterAccess.Value), default);
        }
    }
}
