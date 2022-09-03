using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu.Builder
{
    public abstract class LfuBuilderBase<K, V, TBuilder, TCacheReturn> where TBuilder : LfuBuilderBase<K, V, TBuilder, TCacheReturn>
    {
        internal readonly LfuInfo<K> info;

        protected LfuBuilderBase(LfuInfo<K> info)
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
            return this as TBuilder;
        }

        /// <summary>
        /// Use the specified concurrency level.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the cache concurrently.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithConcurrencyLevel(int concurrencyLevel)
        {
            this.info.ConcurrencyLevel = concurrencyLevel;
            return this as TBuilder;
        }

        /// <summary>
        /// Use the specified scheduler to perform maintenance operations.
        /// </summary>
        /// <param name="scheduler">The scheduler to use for maintenance operations.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithScheduler(IScheduler scheduler)
        {
            this.info.Scheduler = scheduler;
            return this as TBuilder;
        }

        /// <summary>
        /// Use the specified equality comparison implementation to compare keys.
        /// </summary>
        /// <param name="comparer">The equality comparison implementation to use when comparing keys.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithKeyComparer(IEqualityComparer<K> comparer)
        {
            this.info.KeyComparer = comparer;
            return this as TBuilder;
        }

        /// <summary>
        /// Use the specified buffer configuration. Smaller buffers consume less memory, larger buffers can
        /// increase concurrent throughput.
        /// </summary>
        /// <param name="comparer">The buffer configuration to use.</param>
        /// <returns>A ConcurrentLfuBuilder</returns>
        public TBuilder WithBufferConfiguration(LfuBufferSize bufferConfiguration)
        {
            this.info.BufferConfiguration = bufferConfiguration;
            return this as TBuilder;
        }

        /// <summary>
        /// Builds a cache configured via the method calls invoked on the builder instance.
        /// </summary>
        /// <returns>A cache.</returns>
        public abstract TCacheReturn Build();
    }
}
