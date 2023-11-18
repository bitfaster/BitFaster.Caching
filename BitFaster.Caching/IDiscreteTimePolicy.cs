using System;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a per item time based cache policy.
    /// </summary>
    public interface IDiscreteTimePolicy
    {
        /// <summary>
        /// Gets the time to expire for an item in the cache.
        /// </summary>
        /// <param name="key">The key of the item.</param>
        /// <param name="timeToExpire">If the key exists, the time to live for the item with the specified key.</param>
        /// <returns>True if the key exists, otherwise false.</returns>
        bool TryGetTimeToExpire<K>(K key, out TimeSpan timeToExpire);

        /// <summary>
        /// Remove all expired items from the cache.
        /// </summary>
        void TrimExpired();
    }
}
