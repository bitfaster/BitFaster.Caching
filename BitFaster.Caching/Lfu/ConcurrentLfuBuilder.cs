
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
            this.info.ThrowIfExpirySet();
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
            var expiry = info.GetExpiry<V>();

            if (expiry != null)
            {
                return new ConcurrentTLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, expiry);
            }

            return new ConcurrentLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer);
        }
    }
}
