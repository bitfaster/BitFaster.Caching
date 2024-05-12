using System;
using System.Collections.Generic;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu.Builder
{
    /// <summary>
    /// Represents the base class to be extended by LFU builder implementations.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    /// <typeparam name="TBuilder">The type of the builder.</typeparam>
    /// <typeparam name="TCacheReturn">The return type of the builder.</typeparam>
    public abstract class LfuBuilderBase<K, V, TBuilder, TCacheReturn> where TBuilder : LfuBuilderBase<K, V, TBuilder, TCacheReturn>
    {
        internal readonly LfuInfo<K> info;

        internal LfuBuilderBase(LfuInfo<K> info)
        {
            this.info = info;
        }

        /// <summary>
        /// Set the maximum number of values to keep in the cache. If more items than this are added, 
        /// the cache eviction policy will determine which values to remove.
        /// </summary>
        /// <param name="capacity">The maximum number of values to keep in the cache.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithCapacity(int capacity)
        {
            this.info.Capacity = capacity;
            return (this as TBuilder)!;
        }

        /// <summary>
        /// Use the specified concurrency level.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the cache concurrently.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithConcurrencyLevel(int concurrencyLevel)
        {
            this.info.ConcurrencyLevel = concurrencyLevel;
            return (this as TBuilder)!;
        }

        /// <summary>
        /// Use the specified scheduler to perform maintenance operations.
        /// </summary>
        /// <param name="scheduler">The scheduler to use for maintenance operations.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithScheduler(IScheduler scheduler)
        {
            this.info.Scheduler = scheduler;
            return (this as TBuilder)!;
        }

        /// <summary>
        /// Use the specified equality comparison implementation to compare keys.
        /// </summary>
        /// <param name="comparer">The equality comparison implementation to use when comparing keys.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithKeyComparer(IEqualityComparer<K> comparer)
        {
            this.info.KeyComparer = comparer;
            return (this as TBuilder)!;
        }

        /// <summary>
        /// Evict after a fixed duration since an entry's creation or most recent replacement.
        /// </summary>
        /// <param name="expiration">The length of time before an entry is automatically removed.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithExpireAfterWrite(TimeSpan expiration)
        {
            this.info.TimeToExpireAfterWrite = expiration;
            return (this as TBuilder)!;
        }

        /// <summary>
        /// Evict after a fixed duration since an entry's most recent read or write.
        /// </summary>
        /// <param name="expiration">The length of time before an entry is automatically removed.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithExpireAfterAccess(TimeSpan expiration)
        {
            this.info.TimeToExpireAfterAccess = expiration;
            return (this as TBuilder)!;
        }

        /// <summary>
        /// Builds a cache configured via the method calls invoked on the builder instance.
        /// </summary>
        /// <returns>A cache.</returns>
        public abstract TCacheReturn Build();
    }
}
