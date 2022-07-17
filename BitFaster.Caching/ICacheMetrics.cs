using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
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

        //bool IsEnabled { get; }
    }
}
