
namespace BitFaster.Caching
{
    /// <summary>
    /// Represents the cache policy. Cache policy is dependent on the parameters chosen
    /// when constructing the cache.
    /// </summary>
    public class CachePolicy
    {
        /// <summary>
        /// Initializes a new instance of the CachePolicy class with the specified eviction and expire after write policies.
        /// </summary>
        /// <param name="eviction">The eviction policy.</param>
        /// <param name="expireAfterWrite">The expire after write policy.</param>
        public CachePolicy(Optional<IBoundedPolicy> eviction, Optional<ITimePolicy> expireAfterWrite)
        {
            this.Eviction = eviction;
            this.ExpireAfterWrite = expireAfterWrite;
        }

        /// <summary>
        /// Initializes a new instance of the CachePolicy class with the specified policies.
        /// </summary>
        /// <param name="eviction">The eviction policy.</param>
        /// <param name="expireAfterWrite">The expire after write policy.</param>
        /// <param name="expireAfterAccess">The expire after access policy.</param>
        public CachePolicy(Optional<IBoundedPolicy> eviction, Optional<ITimePolicy> expireAfterWrite, Optional<ITimePolicy> expireAfterAccess)
        {
            this.Eviction = eviction;
            this.ExpireAfterWrite = expireAfterWrite;
            this.ExpireAfterAccess = expireAfterAccess;
        }

        /// <summary>
        /// Initializes a new instance of the CachePolicy class with the specified policies.
        /// </summary>
        /// <param name="eviction">The eviction policy.</param>
        /// <param name="expireAfterWrite">The expire after write policy.</param>
        /// <param name="expireAfterAccess">The expire after access policy.</param>
        /// <param name="expireAfter">The expire after policy.</param>
        public CachePolicy(Optional<IBoundedPolicy> eviction, Optional<ITimePolicy> expireAfterWrite, Optional<ITimePolicy> expireAfterAccess, Optional<IDiscreteTimePolicy> expireAfter)
        {
            this.Eviction = eviction;
            this.ExpireAfterWrite = expireAfterWrite;
            this.ExpireAfterAccess = expireAfterAccess;
            this.ExpireAfter = expireAfter;
        }

        /// <summary>
        /// Gets the bounded size eviction policy. This policy evicts items from the cache
        /// if it exceeds capacity.
        /// </summary>
        public Optional<IBoundedPolicy> Eviction { get; }

        /// <summary>
        /// Gets the expire after write policy, if any. This policy evicts items after a 
        /// fixed duration since an entry's creation or most recent replacement.
        /// </summary>
        public Optional<ITimePolicy> ExpireAfterWrite { get; }

        /// <summary>
        /// Gets the expire after access policy, if any. This policy evicts items after a 
        /// fixed duration since an entry's creation or most recent read/write access.
        /// </summary>
        public Optional<ITimePolicy> ExpireAfterAccess { get; }

        /// <summary>
        /// Gets the expire after policy, if any. This policy evicts items after a
        /// variable time to live computed from the key and value.
        /// </summary>
        public Optional<IDiscreteTimePolicy> ExpireAfter { get; }

        public bool TryTrimExpired()
        {
            if (ExpireAfterWrite.HasValue)
            {
                ExpireAfterWrite.Value.TrimExpired();
                return true;
            }

            if (ExpireAfterAccess.HasValue)
            {
                ExpireAfterAccess.Value.TrimExpired();
                return true;
            }

            if (ExpireAfter.HasValue)
            {
                ExpireAfter.Value.TrimExpired();
                return true;
            }

            return false;
        }
    }
}
