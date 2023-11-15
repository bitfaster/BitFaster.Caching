using System;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a fixed time based cache policy.
    /// </summary>
    public interface ITimePolicy
    {
        /// <summary>
        /// Gets the time to expire for items in the cache.
        /// </summary>
        TimeSpan TimeToLive { get; }

        /// <summary>
        /// Remove all expired items from the cache.
        /// </summary>
        void TrimExpired();
    }
}
