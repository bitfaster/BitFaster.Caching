﻿using System;
using System.Collections.Generic;

namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// Parameters for buiding an LRU.
    /// </summary>
    /// <typeparam name="K">The LRU key type</typeparam>
    // backcompat: make class internal
    public sealed class LruInfo<K>
    {
        /// <summary>
        /// Gets or sets the capacity partition.
        /// </summary>
        public ICapacityPartition Capacity { get; set; } = new FavorWarmPartition(128);

        /// <summary>
        /// Gets or sets the concurrency level.
        /// </summary>
        public int ConcurrencyLevel { get; set; } = Defaults.ConcurrencyLevel;

        /// <summary>
        /// Gets or sets the time to expire after write.
        /// </summary>
        public TimeSpan? TimeToExpireAfterWrite { get; set; } = null;

        /// <summary>
        /// Gets or sets a value indicating whether to use metrics.
        /// </summary>
        public bool WithMetrics { get; set; } = false;

        /// <summary>
        /// Gets or sets the KeyComparer.
        /// </summary>
        public IEqualityComparer<K> KeyComparer { get; set; } = EqualityComparer<K>.Default;
    }
}
