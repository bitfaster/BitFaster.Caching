using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    ///<inheritdoc/>
    public sealed class ConcurrentLru<K, V> : TemplateConcurrentLru<K, V, LruItem<K, V>, LruPolicy<K, V>, TelemetryPolicy<K, V>>
    {
        /// <summary>
        /// Initializes a new instance of the ConcurrentLru class with the specified capacity that has the default 
        /// concurrency level, and uses the default comparer for the key type.
        /// </summary>
        /// <param name="capacity">The maximum number of elements that the ConcurrentLru can contain.</param>
        public ConcurrentLru(int capacity)
            : base(Defaults.ConcurrencyLevel, capacity, EqualityComparer<K>.Default, new LruPolicy<K, V>(), new TelemetryPolicy<K, V>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConcurrentLru class that has the specified concurrency level, has the 
        /// specified initial capacity, and uses the specified IEqualityComparer<T>.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the ConcurrentLru concurrently.</param>
        /// <param name="capacity">The maximum number of elements that the ConcurrentLru can contain.</param>
        /// <param name="comparer">The IEqualityComparer<T> implementation to use when comparing keys.</param>
        public ConcurrentLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer)
            : base(concurrencyLevel, capacity, comparer, new LruPolicy<K, V>(), new TelemetryPolicy<K, V>())
        {
        }

        /// <summary>
        /// Gets the ratio of hits to misses, where a value of 1 indicates 100% hits.
        /// </summary>
        public double HitRatio => this.hitCounter.HitRatio;

        /// <summary>
        /// Occurs when an item is removed from the cache.
        /// </summary>
        public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved
        {
            add { this.hitCounter.ItemRemoved += value; }
            remove { this.hitCounter.ItemRemoved -= value; }
        }
    }
}
