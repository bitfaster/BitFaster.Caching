using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents the cache policy. Cache policy is dependent on the parameters chosen
    /// when constructing the cache.
    /// </summary>
    public class CachePolicy
    {
        public CachePolicy(IBoundedPolicy eviction, Optional<ITimePolicy> expireAfterWrite)
        {
            this.Eviction = Optional<IBoundedPolicy>.From(eviction);
            this.ExpireAfterWrite = expireAfterWrite;
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
    }
}
