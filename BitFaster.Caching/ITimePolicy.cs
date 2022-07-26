using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public interface ITimePolicy
    {
        /// <summary>
        /// Gets a value indicating whether the cache can expire items based on time.
        /// </summary>
        bool CanExpire { get; }

        /// <summary>
        /// Gets the time to live for items in the cache.
        /// </summary>
        TimeSpan TimeToLive { get; }

        /// <summary>
        /// Remove all expired items from the cache.
        /// </summary>
        void TrimExpired();
    }
}
