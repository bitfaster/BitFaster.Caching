using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Specifies the reason an item was removed from the Cache.
    /// </summary>
    public enum ItemRemovedReason
    {
        /// <summary>
        /// The item is removed from the cache by a remove method call.
        /// </summary>
        Removed,

        /// <summary>
        /// The item is removed from the cache by the cache eviction policy.
        /// </summary>
        Evicted,
    }
}
