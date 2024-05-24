using System;
using System.Collections.Generic;

namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// Parameters for buiding an LRU.
    /// </summary>
    /// <typeparam name="K">The LRU key type</typeparam>
    // backcompat: make class internal
    public sealed class LruInfo<K>
        where K : notnull
    {
        private object? expiry = null;

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
        /// Gets or sets the time to expire after access.
        /// </summary>
        public TimeSpan? TimeToExpireAfterAccess { get; set; } = null;

        /// <summary>
        /// Set the custom expiry.
        /// </summary>
        /// <param name="expiry">The expiry</param>
        public void SetExpiry<V>(IExpiryCalculator<K, V> expiry) => this.expiry = expiry;

        /// <summary>
        /// Get the custom expiry.
        /// </summary>
        /// <returns>The expiry.</returns>
        public IExpiryCalculator<K, V>? GetExpiry<V>() 
        {
            if (this.expiry == null)
            {
                return null;
            }

            var e = this.expiry as IExpiryCalculator<K, V>;

            if (e == null)                                              
                Throw.InvalidOp($"Incompatible IExpiryCalculator value generic type argument, expected {typeof(IExpiryCalculator<K,V>)} but found {this.expiry.GetType()}");

            return e;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use metrics.
        /// </summary>
        public bool WithMetrics { get; set; } = false;

        /// <summary>
        /// Gets or sets the KeyComparer.
        /// </summary>
        public IEqualityComparer<K> KeyComparer { get; set; } = EqualityComparer<K>.Default;

        internal void ThrowIfExpirySpecified(string extensionName)
        {
            if (this.expiry != null)
                Throw.InvalidOp("WithExpireAfter is not compatible with " + extensionName);
        }
    }
}
