
namespace BitFaster.Caching
{
    /// <summary>
    /// Represents cache metrics collected over the lifetime of the cache.
    /// If metrics are disabled.
    /// </summary>
    public interface ICacheMetrics
    {
        /// <summary>
        /// Gets the ratio of hits to misses, where a value of 1 indicates 100% hits.
        /// </summary>
        double HitRatio { get; }

        /// <summary>
        /// Gets the total number of requests made to the cache.
        /// </summary>
        long Total { get; }

        /// <summary>
        /// Gets the total number of cache hits.
        /// </summary>
        long Hits { get; }

        /// <summary>
        /// Gets the total number of cache misses.
        /// </summary>
        long Misses { get; }

        /// <summary>
        /// Gets the total number of evicted items.
        /// </summary>
        long Evicted { get; }

        // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Gets the total number of updated items.
        /// </summary>
        long Updated => 0;
#endif
    }
}
