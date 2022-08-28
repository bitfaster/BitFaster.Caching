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
        /// Build an IScopedCache. IDisposable values are wrapped in a lifetime scope. Scoped caches return lifetimes that prevent
        /// values from being disposed until the calling code completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>A ScopedConcurrentLruBuilder.</returns>
        public static ScopedConcurrentLfuBuilder<K, V, Scoped<V>> AsScopedCache<K, V>(this ConcurrentLfuBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new ConcurrentLfuBuilder<K, Scoped<V>>(builder.info);
            return new ScopedConcurrentLfuBuilder<K, V, Scoped<V>>(convertBuilder);
        }

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
        /// Build an IAsyncCache, the GetOrAdd method becomes GetOrAddAsync. 
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AtomicConcurrentLfuBuilder to chain method calls onto.</param>
        /// <returns>An AtomicAsyncConcurrentLfuBuilder.</returns>
        public static AtomicAsyncConcurrentLfuBuilder<K, V> AsAsyncCache<K, V>(this AtomicConcurrentLfuBuilder<K, V> builder)
        {
            var convertBuilder = new ConcurrentLfuBuilder<K, AsyncAtomicFactory<K, V>>(builder.info);
            return new AtomicAsyncConcurrentLfuBuilder<K, V>(convertBuilder);
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

        /// <summary>
        /// Execute the cache's GetOrAdd method atomically, such that it is applied at most once per key. Other threads
        /// attempting to update the same key will be blocked until value factory completes. Incurs a small performance
        /// penalty.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AsyncConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AtomicAsyncConcurrentLruBuilder.</returns>
        public static AtomicAsyncConcurrentLfuBuilder<K, V> WithAtomicGetOrAdd<K, V>(this AsyncConcurrentLfuBuilder<K, V> builder)
        {
            var convertBuilder = new ConcurrentLfuBuilder<K, AsyncAtomicFactory<K, V>>(builder.info);
            return new AtomicAsyncConcurrentLfuBuilder<K, V>(convertBuilder);
        }
    }
}
