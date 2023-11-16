
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

        /// <summary>
        /// Evict after a duration calculated for each item using the specified IExpiryCalculator.
        /// </summary>
        /// <param name="expiry">The expiry calculator that determines item time to expire.</param>
        /// <returns>A ConcurrentLruBuilder</returns>
        public ConcurrentLruBuilder<K, V> WithExpireAfter(IExpiryCalculator<K, V> expiry)
        {
            this.info.SetExpiry(expiry);
            return this;
        }

        ///<inheritdoc/>
        public override ICache<K, V> Build()
        {
            return ConcurrentLru.Create<K, V>(this.info);
        }
    }
}
