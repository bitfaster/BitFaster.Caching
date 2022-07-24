using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru.Builder;
using BitFaster.Caching.Synchronized;

namespace BitFaster.Caching.Lru
{
    public static class ConcurrentLruBuilderExtensions
    {
        /// <summary>
        /// Wrap IDisposable values in a lifetime scope. Scoped caches return lifetimes that prevent
        /// values from being disposed until the calling code completes.
        /// </summary>
        /// <typeparam name="K">The type of keys in the cache.</typeparam>
        /// <typeparam name="V">The type of values in the cache.</typeparam>
        /// <param name="builder">The ConcurrentLruBuilder to chain method calls onto.</param>
        /// <returns>A ScopedLruBuilder</returns>
        public static ScopedConcurrentLruBuilder<K, V, Scoped<V>> WithScopedValues<K, V>(this ConcurrentLruBuilder<K, V> builder) where V : IDisposable
        {
            var scoped = new ConcurrentLruBuilder<K, Scoped<V>>(builder.info);
            return new ScopedConcurrentLruBuilder<K, V, Scoped<V>>(scoped);
        }

        public static AtomicConcurrentLruBuilder<K, V> WithAtomicCreate<K, V>(this ConcurrentLruBuilder<K, V> b)
        {
            var a = new ConcurrentLruBuilder<K, AtomicFactory<K, V>>(b.info);
            return new AtomicConcurrentLruBuilder<K, V>(a);
        }

        public static AtomicScopedConcurrentLruBuilder<K, V> WithAtomicCreate<K, V, W>(this ScopedConcurrentLruBuilder<K, V, W> b) where V : IDisposable where W : IScoped<V>
        {
            var atomicScoped = new ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>>(b.info);

            return new AtomicScopedConcurrentLruBuilder<K, V>(atomicScoped);
        }

        public static AtomicScopedConcurrentLruBuilder<K, V> WithScopedValues<K, V>(this AtomicConcurrentLruBuilder<K, V> b) where V : IDisposable
        {
            var atomicScoped = new ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>>(b.info);
            return new AtomicScopedConcurrentLruBuilder<K, V>(atomicScoped);
        }

        public static AsyncConcurrentLruBuilder<K, V> AsAsyncCache<K, V>(this ConcurrentLruBuilder<K, V> builder)
        {
            return new AsyncConcurrentLruBuilder<K, V>(builder.info);
        }

        public static ScopedAsyncConcurrentLruBuilder<K, V> WithScopedValues<K, V>(this AsyncConcurrentLruBuilder<K, V> b) where V : IDisposable
        {
            var asyncScoped = new AsyncConcurrentLruBuilder<K, Scoped<V>>(b.info);
            return new ScopedAsyncConcurrentLruBuilder<K, V>(asyncScoped);
        }

        public static ScopedAsyncConcurrentLruBuilder<K, V> AsAsyncCache<K, V>(this ScopedConcurrentLruBuilder<K, V, Scoped<V>> b) where V : IDisposable
        {
            var asyncScoped = new AsyncConcurrentLruBuilder<K, Scoped<V>>(b.info);
            return new ScopedAsyncConcurrentLruBuilder<K, V>(asyncScoped);
        }

        public static AtomicAsyncConcurrentLruBuilder<K, V> AsAsyncCache<K, V>(this AtomicConcurrentLruBuilder<K, V> b)
        {
            var a = new ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>>(b.info);
            return new AtomicAsyncConcurrentLruBuilder<K, V>(a);
        }

        public static AtomicAsyncConcurrentLruBuilder<K, V> WithAtomicCreate<K, V>(this AsyncConcurrentLruBuilder<K, V> b)
        {
            var a = new ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>>(b.info);
            return new AtomicAsyncConcurrentLruBuilder<K, V>(a);
        }

        public static AtomicScopedAsyncConcurrentLruBuilder<K, V> AsAsyncCache<K, V>(this AtomicScopedConcurrentLruBuilder<K, V> b) where V : IDisposable
        {
            var a = new AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>>(b.info);
            return new AtomicScopedAsyncConcurrentLruBuilder<K, V>(a);
        }

        public static AtomicScopedAsyncConcurrentLruBuilder<K, V> WithScopedValues<K, V>(this AtomicAsyncConcurrentLruBuilder<K, V> b) where V : IDisposable
        {
            var a = new AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>>(b.info);
            return new AtomicScopedAsyncConcurrentLruBuilder<K, V>(a);
        }

        public static AtomicScopedAsyncConcurrentLruBuilder<K, V> WithAtomicCreate<K, V>(this ScopedAsyncConcurrentLruBuilder<K, V> b) where V : IDisposable
        {
            var a = new AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>>(b.info);
            return new AtomicScopedAsyncConcurrentLruBuilder<K, V>(a);
        }
    }
}
