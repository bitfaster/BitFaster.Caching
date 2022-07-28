using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lru
{
    ///<inheritdoc/>
    public sealed class FastConcurrentLru<K, V> : ConcurrentLruCore<K, V, LruItem<K, V>, LruPolicy<K, V>, NoTelemetryPolicy<K, V>>
    {
        /// <summary>
        /// Initializes a new instance of the FastConcurrentLru class with the specified capacity that has the default 
        /// concurrency level, and uses the default comparer for the key type.
        /// </summary>
        /// <param name="capacity">The maximum number of elements that the FastConcurrentLru can contain.</param>
        public FastConcurrentLru(int capacity)
            : base(Defaults.ConcurrencyLevel, new FavorWarmPartition(capacity), EqualityComparer<K>.Default, default, default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FastConcurrentLru class that has the specified concurrency level, has the 
        /// specified initial capacity, and uses the specified IEqualityComparer<T>.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the FastConcurrentLru concurrently.</param>
        /// <param name="capacity">The maximum number of elements that the FastConcurrentLru can contain.</param>
        /// <param name="comparer">The IEqualityComparer<T> implementation to use when comparing keys.</param>
        public FastConcurrentLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer)
            : base(concurrencyLevel, new FavorWarmPartition(capacity), comparer, default, default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FastConcurrentLru class that has the specified concurrency level, has the 
        /// specified initial capacity, and uses the specified IEqualityComparer<T>.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the FastConcurrentLru concurrently.</param>
        /// <param name="capacity">The maximum number of elements that the FastConcurrentLru can contain.</param>
        /// <param name="comparer">The IEqualityComparer<T> implementation to use when comparing keys.</param>
        public FastConcurrentLru(int concurrencyLevel, ICapacityPartition capacity, IEqualityComparer<K> comparer)
            : base(concurrencyLevel, capacity, comparer, default, default)
        {
        }
    }
}
