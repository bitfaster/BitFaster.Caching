
namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents a telemetry policy.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    public interface ITelemetryPolicy<K, V> : ICacheMetrics, ICacheEvents<K, V>
    {
        /// <summary>
        /// Increment the miss counter.
        /// </summary>
        void IncrementMiss();

        /// <summary>
        /// Increment the hit counter.
        /// </summary>
        void IncrementHit();

        /// <summary>
        /// Register the removal of an item.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="reason">The reason for removal.</param>
        void OnItemRemoved(K key, V value, ItemRemovedReason reason);

        /// <summary>
        /// Register the update of an item.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        void OnItemUpdated(K key, V value);

        /// <summary>
        /// Set the event source for any events that are fired.
        /// </summary>
        /// <param name="source">The event source.</param>
        void SetEventSource(object source);
    }
}
