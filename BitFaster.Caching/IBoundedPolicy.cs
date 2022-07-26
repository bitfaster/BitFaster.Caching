using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public interface IBoundedPolicy
    {
        /// <summary>
        /// Gets the total number of items that can be stored in the cache.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Trim the specified number of items from the cache.
        /// </summary>
        /// <param name="itemCount">The number of items to remove.</param>
        void Trim(int itemCount);
    }
}
