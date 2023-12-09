using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// Specifies the reason an item was removed from the Cache.
    /// </summary>
    public enum ItemRemovedReason
    {
        /// <summary>
        /// The item was removed from the cache by a remove method call.
        /// </summary>
        Removed,

        /// <summary>
        /// The item was removed from the cache by the cache eviction policy.
        /// </summary>
        Evicted,

        /// <summary>
        /// The item was removed from the cache by a clear method call.
        /// </summary>
        Cleared,

        /// <summary>
        /// The item was removed from the cache by a trim method call.
        /// </summary>
        Trimmed,
    }
}
