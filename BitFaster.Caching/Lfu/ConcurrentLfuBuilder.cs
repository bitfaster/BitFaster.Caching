
using BitFaster.Caching.Lfu.Builder;

namespace BitFaster.Caching.Lfu
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
    ///   <item><description>Scoped IDisposable values.</description></item>
    ///   <item><description>Atomic value factory.</description></item>
    /// </list>
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    public sealed class ConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, ConcurrentLfuBuilder<K, V>, ICache<K, V>>
        where K : notnull
    {
        /// <summary>
        /// Creates a ConcurrentLfuBuilder. Chain method calls onto ConcurrentLfuBuilder to configure the cache then call Build to create a cache instance.
        /// </summary>
        public ConcurrentLfuBuilder()
            : base(new LfuInfo<K>())
        {
        }

        internal ConcurrentLfuBuilder(LfuInfo<K> info)
            : base(info)
        {
        }

        /// <summary>
        /// Evict after a duration calculated for each item using the specified IExpiryCalculator.
        /// </summary>
        /// <param name="expiry">The expiry calculator that determines item time to expire.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public ConcurrentLfuBuilder<K, V> WithExpireAfter(IExpiryCalculator<K, V> expiry)
        {
            this.info.SetExpiry(expiry);
            return this;
        }

        ///<inheritdoc/>
        public override ICache<K, V> Build()
        {
            return ConcurrentLfuFactory.Create<K, V>(this.info);
        }
    }

    internal static class ConcurrentLfuFactory
    {
        internal static ICache<K, V> Create<K, V>(LfuInfo<K> info)
            where K : notnull
        {
            if (info.TimeToExpireAfterWrite.HasValue && info.TimeToExpireAfterAccess.HasValue)
                Throw.InvalidOp("Specifying both ExpireAfterWrite and ExpireAfterAccess is not supported.");

            var expiry = info.GetExpiry<V>();

            if (info.TimeToExpireAfterWrite.HasValue && expiry != null)
                Throw.InvalidOp("Specifying both ExpireAfterWrite and ExpireAfter is not supported.");

            if (info.TimeToExpireAfterAccess.HasValue && expiry != null)
                Throw.InvalidOp("Specifying both ExpireAfterAccess and ExpireAfter is not supported.");

            return (info.TimeToExpireAfterWrite.HasValue, info.TimeToExpireAfterAccess.HasValue, expiry != null, info.WithEvents) switch
            {
                // time expiry, with events
                (true, false, false, true) => new ConcurrentTLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, new ExpireAfterWrite<K, V>(info.TimeToExpireAfterWrite!.Value)),
                (false, true, false, true) => new ConcurrentTLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, new ExpireAfterAccess<K, V>(info.TimeToExpireAfterAccess!.Value)),
                (false, false, true, true) => new ConcurrentTLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, expiry!),

                // time expiry, without events
                (true, false, false, false) => new FastConcurrentLfu<K, V, TimeOrderNode<K, V>, ExpireAfterPolicy<K, V, NoEventPolicy<K, V>>>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, new ExpireAfterPolicy<K, V, NoEventPolicy<K, V>>(new ExpireAfterWrite<K, V>(info.TimeToExpireAfterWrite!.Value))),
                (false, true, false, false) => new FastConcurrentLfu<K, V, TimeOrderNode<K, V>, ExpireAfterPolicy<K, V, NoEventPolicy<K, V>>>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, new ExpireAfterPolicy<K, V, NoEventPolicy<K, V>>(new ExpireAfterAccess<K, V>(info.TimeToExpireAfterAccess!.Value))),
                (false, false, true, false) => new FastConcurrentLfu<K, V, TimeOrderNode<K, V>, ExpireAfterPolicy<K, V, NoEventPolicy<K, V>>>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, new ExpireAfterPolicy<K, V, NoEventPolicy<K, V>>(expiry!)),

                // no time expiry, without events
                (false, false, false, false) => new FastConcurrentLfu<K, V, AccessOrderNode<K, V>, AccessOrderPolicy<K, V, NoEventPolicy<K, V>>>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer),

                // no time expiry, with events
               _ => new ConcurrentLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer)
            };
        }
    }
}
