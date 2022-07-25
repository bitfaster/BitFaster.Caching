using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru.Builder;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lru
{
    public static class ConcurrentLruBuilderExtensions
    {
        /// <summary>
        /// Build an IScopedCache. IDisposable values are wrapped in a lifetime scope. Scoped caches return lifetimes that prevent
        /// values from being disposed until the calling code completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>A ScopedConcurrentLruBuilder.</returns>
        public static ScopedConcurrentLruBuilder<K, V, Scoped<V>> AsScopedCache<K, V>(this ConcurrentLruBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new ConcurrentLruBuilder<K, Scoped<V>>(builder.info);
            return new ScopedConcurrentLruBuilder<K, V, Scoped<V>>(convertBuilder);
        }

        /// <summary>
        /// Build an IScopedCache. IDisposable values are wrapped in a lifetime scope. Scoped caches return lifetimes that prevent
        /// values from being disposed until the calling code completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AtomicConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AtomicScopedConcurrentLruBuilder.</returns>
        public static AtomicScopedConcurrentLruBuilder<K, V> AsScopedCache<K, V>(this AtomicConcurrentLruBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>>(builder.info);
            return new AtomicScopedConcurrentLruBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Build an IScopedAsyncCache. IDisposable values are wrapped in a lifetime scope. Scoped caches return lifetimes that prevent
        /// values from being disposed until the calling code completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AsyncConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>A ScopedAsyncConcurrentLruBuilder.</returns>
        public static ScopedAsyncConcurrentLruBuilder<K, V> AsScopedCache<K, V>(this AsyncConcurrentLruBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new AsyncConcurrentLruBuilder<K, Scoped<V>>(builder.info);
            return new ScopedAsyncConcurrentLruBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Build an IScopedAsyncCache. IDisposable values are wrapped in a lifetime scope. Scoped caches return lifetimes that prevent
        /// values from being disposed until the calling code completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AtomicAsyncConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AtomicScopedAsyncConcurrentLruBuilder.</returns>
        public static AtomicScopedAsyncConcurrentLruBuilder<K, V> AsScopedCache<K, V>(this AtomicAsyncConcurrentLruBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>>(builder.info);
            return new AtomicScopedAsyncConcurrentLruBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Execute the cache's GetOrAdd value factory atomically, such that it is applied at most once per key. Other threads
        /// attempting to update the same key will be blocked until value factory completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AtomicConcurrentLruBuilder.</returns>
        public static AtomicConcurrentLruBuilder<K, V> WithAtomicValueFactory<K, V>(this ConcurrentLruBuilder<K, V> builder)
        {
            var convertBuilder = new ConcurrentLruBuilder<K, AtomicFactory<K, V>>(builder.info);
            return new AtomicConcurrentLruBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Execute the cache's ScopedGetOrAdd value factory atomically, such that it is applied at most once per key. Other threads
        /// attempting to update the same key will be blocked until value factory completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <typeparam name="W">The wrapped value type.</typeparam>
        /// <param name="builder">The ScopedConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AtomicScopedConcurrentLruBuilder.</returns>
        public static AtomicScopedConcurrentLruBuilder<K, V> WithAtomicValueFactory<K, V, W>(this ScopedConcurrentLruBuilder<K, V, W> builder) where V : IDisposable where W : IScoped<V>
        {
            var convertBuilder = new ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>>(builder.info);
            return new AtomicScopedConcurrentLruBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Execute the cache's GetOrAddAsync value factory atomically, such that it is applied at most once per key. Other threads
        /// attempting to update the same key will wait on the same value factory task.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AsyncConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AtomicAsyncConcurrentLruBuilder.</returns>
        public static AtomicAsyncConcurrentLruBuilder<K, V> WithAtomicValueFactory<K, V>(this AsyncConcurrentLruBuilder<K, V> builder)
        {
            var convertBuilder = new ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>>(builder.info);
            return new AtomicAsyncConcurrentLruBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Execute the cache's ScopedGetOrAddAsync value factory atomically, such that it is applied at most once per key. Other threads
        /// attempting to update the same key will wait on the same value factory task.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ScopedAsyncConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AtomicScopedAsyncConcurrentLruBuilder.</returns>
        public static AtomicScopedAsyncConcurrentLruBuilder<K, V> WithAtomicValueFactory<K, V>(this ScopedAsyncConcurrentLruBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>>(builder.info);
            return new AtomicScopedAsyncConcurrentLruBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Build an IAsyncCache, the GetOrAdd method becomes GetOrAddAsync. 
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AsyncConcurrentLruBuilder.</returns>
        public static AsyncConcurrentLruBuilder<K, V> AsAsyncCache<K, V>(this ConcurrentLruBuilder<K, V> builder)
        {
            return new AsyncConcurrentLruBuilder<K, V>(builder.info);
        }

        /// <summary>
        /// Build an IScopedAsyncCache, the ScopedGetOrAdd method becomes ScopedGetOrAddAsync. 
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ScopedConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>A ScopedAsyncConcurrentLruBuilder.</returns>
        public static ScopedAsyncConcurrentLruBuilder<K, V> AsAsyncCache<K, V>(this ScopedConcurrentLruBuilder<K, V, Scoped<V>> builder) where V : IDisposable
        {
            var convertBuilder = new AsyncConcurrentLruBuilder<K, Scoped<V>>(builder.info);
            return new ScopedAsyncConcurrentLruBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Build an IAsyncCache, the GetOrAdd method becomes GetOrAddAsync. 
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AtomicConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AtomicAsyncConcurrentLruBuilder.</returns>
        public static AtomicAsyncConcurrentLruBuilder<K, V> AsAsyncCache<K, V>(this AtomicConcurrentLruBuilder<K, V> builder)
        {
            var convertBuilder = new ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>>(builder.info);
            return new AtomicAsyncConcurrentLruBuilder<K, V>(convertBuilder);
        }

        /// <summary>
        /// Build an IScopedAsyncCache, the ScopedGetOrAdd method becomes ScopedGetOrAddAsync.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The AtomicScopedConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>An AtomicScopedAsyncConcurrentLruBuilder.</returns>
        public static AtomicScopedAsyncConcurrentLruBuilder<K, V> AsAsyncCache<K, V>(this AtomicScopedConcurrentLruBuilder<K, V> builder) where V : IDisposable
        {
            var convertBuilder = new AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>>(builder.info);
            return new AtomicScopedAsyncConcurrentLruBuilder<K, V>(convertBuilder);
        }
    }
}
