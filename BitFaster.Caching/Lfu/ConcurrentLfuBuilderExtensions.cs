using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lfu.Builder;
using BitFaster.Caching.Lru.Builder;
using BitFaster.Caching.Lru;

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
        /// <param name="builder">The ConcurrentLfuBuilder to chain method calls onto.</param>
        /// <returns>A ScopedConcurrentLfuBuilder.</returns>
        public static ScopedConcurrentLfuBuilder<K, V, Scoped<V>> AsScopedCache<K, V>(this ConcurrentLfuBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new ConcurrentLfuBuilder<K, Scoped<V>>(builder.info);
            return new ScopedConcurrentLfuBuilder<K, V, Scoped<V>>(convertBuilder);
        }

        /// <summary>
        /// Build an IScopedAsyncCache. IDisposable values are wrapped in a lifetime scope. Scoped caches return lifetimes that prevent
        /// values from being disposed until the calling code completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AsyncConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>A ScopedAsyncConcurrentLruBuilder.</returns>
        public static ScopedAsyncConcurrentLfuBuilder<K, V> AsScopedCache<K, V>(this AsyncConcurrentLfuBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new AsyncConcurrentLfuBuilder<K, Scoped<V>>(builder.info);
            return new ScopedAsyncConcurrentLfuBuilder<K, V>(convertBuilder);
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
        /// Build an IScopedAsyncCache, the ScopedGetOrAdd method becomes ScopedGetOrAddAsync. 
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ScopedConcurrentLfuBuilder to chain method calls onto.</param>
        /// <returns>A ScopedAsyncConcurrentLfuBuilder.</returns>
        public static ScopedAsyncConcurrentLfuBuilder<K, V> AsAsyncCache<K, V>(this ScopedConcurrentLfuBuilder<K, V, Scoped<V>> builder) where V : IDisposable
        {
            var convertBuilder = new AsyncConcurrentLfuBuilder<K, Scoped<V>>(builder.info);
            return new ScopedAsyncConcurrentLfuBuilder<K, V>(convertBuilder);
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
        /// <param name="builder">The AsyncConcurrentLfuBuilder to chain method calls onto.</param>
        /// <returns>An AtomicAsyncConcurrentLfuBuilder.</returns>
        public static AtomicAsyncConcurrentLfuBuilder<K, V> WithAtomicGetOrAdd<K, V>(this AsyncConcurrentLfuBuilder<K, V> builder)
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
        /// <typeparam name="W">The wrapped value type.</typeparam>
        /// <param name="builder">The ScopedConcurrentLfuBuilder to chain method calls onto.</param>
        /// <returns>An AtomicScopedConcurrentLfuBuilder.</returns>
        public static AtomicScopedConcurrentLfuBuilder<K, V> WithAtomicGetOrAdd<K, V, W>(this ScopedConcurrentLfuBuilder<K, V, W> builder) where V : IDisposable where W : IScoped<V>
        {
            var convertBuilder = new ConcurrentLfuBuilder<K, ScopedAtomicFactory<K, V>>(builder.info);
            return new AtomicScopedConcurrentLfuBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Build an IScopedCache. IDisposable values are wrapped in a lifetime scope. Scoped caches return lifetimes that prevent
        /// values from being disposed until the calling code completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AtomicConcurrentLfuBuilder to chain method calls onto.</param>
        /// <returns>An AtomicScopedConcurrentLfuBuilder.</returns>
        public static AtomicScopedConcurrentLfuBuilder<K, V> AsScopedCache<K, V>(this AtomicConcurrentLfuBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new ConcurrentLfuBuilder<K, ScopedAtomicFactory<K, V>>(builder.info);
            return new AtomicScopedConcurrentLfuBuilder<K, V>(convertBuilder);
        }
    }
}
