namespace BitFaster.Caching
{
    /// <summary>
    /// Calculates the weight of cache entries. Weights are relative, have no unit, and bound the total
    /// cache size instead of the number of entries.
    /// </summary>
    /// <typeparam name="K">The type of keys.</typeparam>
    /// <typeparam name="V">The type of values.</typeparam>
    /// <remarks>
    /// Implementations must be thread-safe. Weights are calculated on creation and replacement, not
    /// when a cached value is mutated in place. Zero-weight entries are not evicted for capacity.
    /// </remarks>
    public interface IWeightCalculator<K, V>
    {
        /// <summary>
        /// Returns the non-negative weight of a cache entry.
        /// </summary>
        /// <param name="key">The key to weigh.</param>
        /// <param name="value">The value to weigh.</param>
        /// <returns>The weight of the entry.</returns>
        int GetWeight(K key, V value);
    }
}
