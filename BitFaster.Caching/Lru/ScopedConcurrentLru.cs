using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public class ScopedConcurrentLru<K, V> : TemplateConcurrentLru<K, V, Scoped<V>, LruItem<K, Scoped<V>>, ScopedLruPolicy<K, V>, HitCounter> where V : IDisposable
    {
        public ScopedConcurrentLru(int capacity)
            : base(Defaults.ConcurrencyLevel, capacity, EqualityComparer<K>.Default, new ScopedLruPolicy<K, V>(), new HitCounter())
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConcurrentLru class that has the specified concurrency level, has the 
        /// specified initial capacity, and uses the specified IEqualityComparer<T>.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the ConcurrentLru concurrently.</param>
        /// <param name="capacity">The maximum number of elements that the ConcurrentLru can contain.</param>
        /// <param name="comparer">The IEqualityComparer<T> implementation to use when comparing keys.</param>
        public ScopedConcurrentLru(int concurrencyLevel, int capacity, IEqualityComparer<K> comparer)
            : base(concurrencyLevel, capacity, comparer, new ScopedLruPolicy<K, V>(), new HitCounter())
        {
        }

        /// <summary>
        /// Gets the ratio of hits to misses, where a value of 1 indicates 100% hits.
        /// </summary>
        public double HitRatio => this.hitCounter.HitRatio;


        // Now it is possible to create these methods as extensions without value factory wrappers

        public Lifetime<V> ScopedGetOrAdd(K key, Func<K, V> valueFactory)
        {
            while (true)
            {
                var scope = this.GetOrAdd(key, valueFactory);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }

        public async Task<Lifetime<V>> ScopedGetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            while (true)
            {
                var scope = await this.GetOrAddAsync(key, valueFactory);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }
    }
}
