using System;
using System.Collections.Generic;

namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// Recursive generic base class enables builder inheritance.
    /// </summary>
    public abstract class LruBuilderBase<K, V, TBuilder, TCacheReturn> where TBuilder : LruBuilderBase<K, V, TBuilder, TCacheReturn>
    {
        internal readonly LruInfo<K> info;

        internal LruBuilderBase(LruInfo<K> info)
        {
            this.info = info;
        }

        /// <summary>
        /// Set the maximum number of values to keep in the cache. If more items than this are added, 
        /// the cache eviction policy will determine which values to remove.
        /// </summary>
        /// <param name="capacity">The maximum number of values to keep in the cache.</param>
        /// <returns>A ConcurrentLruBuilder</returns>
        public TBuilder WithCapacity(int capacity)
        {
            this.info.Capacity = new FavorWarmPartition(capacity);
            return this as TBuilder;
        }

        /// <summary>
        /// Set the maximum number of values to keep in the cache. If more items than this are added, 
        /// the cache eviction policy will determine which values to remove.
        /// </summary>
        /// <param name="capacity">The capacity partition scheme to use.</param>
        /// <returns>A ConcurrentLruBuilder</returns>
        public TBuilder WithCapacity(ICapacityPartition capacity)
        {
            this.info.Capacity = capacity;
            return this as TBuilder;
        }

        /// <summary>
        /// Use the specified concurrency level.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the cache concurrently.</param>
        /// <returns>A ConcurrentLruBuilder</returns>
        public TBuilder WithConcurrencyLevel(int concurrencyLevel)
        {
            this.info.ConcurrencyLevel = concurrencyLevel;
            return this as TBuilder;
        }

        /// <summary>
        /// Use the specified equality comparison implementation to compare keys.
        /// </summary>
        /// <param name="comparer">The equality comparison implementation to use when comparing keys.</param>
        /// <returns>A ConcurrentLruBuilder</returns>
        public TBuilder WithKeyComparer(IEqualityComparer<K> comparer)
        {
            this.info.KeyComparer = comparer;
            return this as TBuilder;
        }

        /// <summary>
        /// Collect cache metrics, such as Hit rate. Metrics have a small performance penalty.
        /// </summary>
        /// <returns>A ConcurrentLruBuilder</returns>
        public TBuilder WithMetrics()
        {
            this.info.WithMetrics = true;
            return this as TBuilder;
        }

        /// <summary>
        /// Evict after a fixed duration since an entry's creation or most recent replacement.
        /// </summary>
        /// <param name="expiration">The length of time before an entry is automatically removed.</param>
        /// <returns>A ConcurrentLruBuilder</returns>
        public TBuilder WithExpireAfterWrite(TimeSpan expiration)
        {
            this.info.TimeToExpireAfterWrite = expiration;
            return this as TBuilder;
        }

        /// <summary>
        /// Builds a cache configured via the method calls invoked on the builder instance.
        /// </summary>
        /// <returns>A cache.</returns>
        public abstract TCacheReturn Build();
    }
}
