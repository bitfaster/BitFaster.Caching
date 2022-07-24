using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    ///<inheritdoc/>
    public sealed class ConcurrentTLru<K, V> : TemplateConcurrentLru<K, V, LongTickCountLruItem<K, V>, TLruLongTicksPolicy<K, V>, TelemetryPolicy<K, V>>
    {
        /// <summary>
        /// Initializes a new instance of the ConcurrentTLru class with the specified capacity and time to live that has the default 
        /// concurrency level, and uses the default comparer for the key type.
        /// </summary>
        /// <param name="capacity">The maximum number of elements that the ConcurrentTLru can contain.</param>
        /// <param name="timeToLive">The time to live for cached values.</param>
        public ConcurrentTLru(int capacity, TimeSpan timeToLive)
            : base(Defaults.ConcurrencyLevel, new FavorWarmPartition(capacity), EqualityComparer<K>.Default, new TLruLongTicksPolicy<K, V>(timeToLive), default)
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
            : base(concurrencyLevel, new FavorWarmPartition(capacity), comparer, new TLruLongTicksPolicy<K, V>(timeToLive), default)
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
        public ConcurrentTLru(int concurrencyLevel, ICapacityPartition capacity, IEqualityComparer<K> comparer, TimeSpan timeToLive)
            : base(concurrencyLevel, capacity, comparer, new TLruLongTicksPolicy<K, V>(timeToLive), default)
        {
        }

        /// <summary>
        /// Remove all expired items from the cache.
        /// </summary>
        /// <remarks>O(n) where n is the number of items in the cache.</remarks>
        public void TrimExpired()
        {
            this.TrimAllDiscardedItems();
        }
    }
}
