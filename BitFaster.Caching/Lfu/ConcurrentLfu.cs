using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu
{
    /// <inheritdoc/>
    [DebuggerTypeProxy(typeof(ConcurrentLfuCore<,>.LfuDebugView))]
    [DebuggerDisplay("Count = {Count}/{Capacity}")]
    public sealed class ConcurrentLfu<K, V> : ConcurrentLfuCore<K, V>
    {
        /// <summary>
        /// Initializes a new instance of the ConcurrentLfu class with the specified capacity.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        public ConcurrentLfu(int capacity)
            : base(capacity)
        { }

        /// <summary>
        /// Initializes a new instance of the ConcurrentLfu class with the specified concurrencyLevel, capacity, scheduler, equality comparer and buffer size.
        /// </summary>
        /// <param name="concurrencyLevel">The concurrency level.</param>
        /// <param name="capacity">The capacity.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <param name="comparer">The equality comparer.</param>
        public ConcurrentLfu(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer)
            : base(concurrencyLevel, capacity, scheduler, comparer)
        {
        }
    }
}
