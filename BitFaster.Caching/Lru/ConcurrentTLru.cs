using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    ///<inheritdoc/>
    public sealed class ConcurrentTLru<K, V> : TemplateConcurrentLru<K, V, LongTickCountLruItem<K, V>, TLruLongTicksPolicy<K, V>, HitCounter>
    {
        /// <summary>
        /// Initializes a new instance of the ConcurrentTLru class with the specified capacity and time to live that has the default 
        /// concurrency level, and uses the default comparer for the key type.
        /// </summary>
        /// <param name="capacity">The maximum number of elements that the ConcurrentTLru can contain.</param>
        /// <param name="timeToLive">The time to live for cached values.</param>
        public ConcurrentTLru(int capacity, TimeSpan timeToLive)
            : base(Defaults.ConcurrencyLevel, capacity, EqualityComparer<K>.Default, new TLruLongTicksPolicy<K, V>(timeToLive), new HitCounter())
        { 
        }

        /// <summary>
        /// Initializes a new instance of the ConcurrentTLru class that has the specified concurrency level, has the 
        /// specified initial capacity, uses the specified IEqualityComparer<T>, and has the specified time to live.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the ConcurrentTLru concurrently.</param>
        /// <param name="capacity">The maximum number of elements that the ConcurrentTLru can contain.</param>
        /// <param name="comparer">The IEqualityComparer<T> implementation to use when comparing keys.</param>
        /// <param name="timeToLive">The time to live for cached values.</param>
        public ConcurrentTLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer, TimeSpan timeToLive)
            : base(concurrencyLevel, capacity, comparer, new TLruLongTicksPolicy<K, V>(timeToLive), new HitCounter())
        {
        }

        /// <summary>
        /// Gets the ratio of hits to misses, where a value of 1 indicates 100% hits.
        /// </summary>
        public double HitRatio => this.hitCounter.HitRatio;
    }
}
