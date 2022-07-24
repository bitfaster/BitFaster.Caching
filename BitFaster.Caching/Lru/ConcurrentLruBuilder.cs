using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru.Builder;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// A builder of ICache and IScopedCache instances with the following configuration
    /// settings:
    /// - The maximum size.
    /// - The concurrency level.
    /// - The key comparer.
    /// 
    /// The following features can be selected which change the underlying cache implementation: 
    /// - Collect metrics (e.g. hit rate). Small perf penalty.
    /// - Time based expiration, measured since write.
    /// - Scoped IDisposable values.
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
