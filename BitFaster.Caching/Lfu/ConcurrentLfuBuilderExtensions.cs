using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lfu.Builder;

namespace BitFaster.Caching.Lfu
{
    public static class ConcurrentLfuBuilderExtensions
    {
        /// <summary>
        /// Build an IAsyncCache, the GetOrAdd method becomes GetOrAddAsync. 
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ConcurrentLfuBuilder to chain method calls onto.</param>
        /// <returns>An AsyncConcurrentLfuBuilder.</returns>
        public static AsyncConcurrentLfuBuilder<K, V> AsAsyncCache<K, V>(this ConcurrentLfuBuilder<K, V> builder)
        {
            return new AsyncConcurrentLfuBuilder<K, V>(builder.info);
        }

        /// <summary>
        /// Execute the cache's GetOrAdd method atomically, such that it is applied at most once per key. Other threads
        /// attempting to update the same key will be blocked until value factory completes. Incurs a small performance
        /// penalty.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ConcurrentLfuBuilder to chain method calls onto.</param>
        /// <returns>An AtomicConcurrentLfuBuilder.</returns>
        public static AtomicConcurrentLfuBuilder<K, V> WithAtomicGetOrAdd<K, V>(this ConcurrentLfuBuilder<K, V> builder)
        {
            var convertBuilder = new ConcurrentLfuBuilder<K, AtomicFactory<K, V>>(builder.info);
            return new AtomicConcurrentLfuBuilder<K, V>(convertBuilder);
        }
    }
}
