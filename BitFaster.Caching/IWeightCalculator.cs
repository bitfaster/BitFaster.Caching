namespace BitFaster.Caching
{
    /// <summary>
    /// Calculates the weight of cache entries. The total weight is used to bound the cache size,
    /// and to determine when an eviction is required. Entry weights are relative to each other and
    /// have no unit.
    /// </summary>
    /// <typeparam name="K">The type of keys.</typeparam>
    /// <typeparam name="V">The type of values.</typeparam>
    public interface IWeightCalculator<K, V>
    {
        /// <summary>
        /// Returns the weight of a cache entry. The weight must be non-negative.
        /// </summary>
        /// <param name="key">The key to weigh.</param>
        /// <param name="value">The value to weigh.</param>
        /// <returns>The weight of the entry.</returns>
        int GetWeight(K key, V value);
    }
}
