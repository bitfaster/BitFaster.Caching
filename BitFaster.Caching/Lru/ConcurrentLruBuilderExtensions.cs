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
        public static ScopedLruBuilder<K, V, Scoped<V>> WithScopedValues<K, V>(this ConcurrentLruBuilder<K, V> builder) where V : IDisposable
        {
            var scoped = new ConcurrentLruBuilder<K, Scoped<V>>(builder.info);
            return new ScopedLruBuilder<K, V, Scoped<V>>(scoped);
        }

        public static AtomicLruBuilder<K, V> WithAtomicCreate<K, V>(this ConcurrentLruBuilder<K, V> b)
        {
            var a = new ConcurrentLruBuilder<K, AtomicFactory<K, V>>(b.info);
            return new AtomicLruBuilder<K, V>(a);
        }

        public static ScopedAtomicLruBuilder<K, V> WithAtomicCreate<K, V, W>(this ScopedLruBuilder<K, V, W> b) where V : IDisposable where W : IScoped<V>
        {
            var atomicScoped = new ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>>(b.info);

            return new ScopedAtomicLruBuilder<K, V>(atomicScoped);
        }

        public static ScopedAtomicLruBuilder<K, V> WithScopedValues<K, V>(this AtomicLruBuilder<K, V> b) where V : IDisposable
        {
            var atomicScoped = new ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>>(b.info);
            return new ScopedAtomicLruBuilder<K, V>(atomicScoped);
        }

        public static AsyncConcurrentLruBuilder<K, V> AsAsyncCache<K, V>(this ConcurrentLruBuilder<K, V> builder)
        {
            return new AsyncConcurrentLruBuilder<K, V>(builder.info);
        }

        public static ScopedAsyncLruBuilder<K, V> WithScopedValues<K, V>(this AsyncConcurrentLruBuilder<K, V> b) where V : IDisposable
        {
            var asyncScoped = new AsyncConcurrentLruBuilder<K, Scoped<V>>(b.info);
            return new ScopedAsyncLruBuilder<K, V>(asyncScoped);
        }

        public static ScopedAsyncLruBuilder<K, V> AsAsyncCache<K, V>(this ScopedLruBuilder<K, V, Scoped<V>> b) where V : IDisposable
        {
            var asyncScoped = new AsyncConcurrentLruBuilder<K, Scoped<V>>(b.info);
            return new ScopedAsyncLruBuilder<K, V>(asyncScoped);
        }

        public static AsyncAtomicLruBuilder<K, V> AsAsyncCache<K, V>(this AtomicLruBuilder<K, V> b)
        {
            var a = new ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>>(b.info);
            return new AsyncAtomicLruBuilder<K, V>(a);
        }

        public static AsyncAtomicLruBuilder<K, V> WithAtomicCreate<K, V>(this AsyncConcurrentLruBuilder<K, V> b)
        {
            var a = new ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>>(b.info);
            return new AsyncAtomicLruBuilder<K, V>(a);
        }

        public static ScopedAsyncAtomicLruBuilder<K, V> AsAsyncCache<K, V>(this ScopedAtomicLruBuilder<K, V> b) where V : IDisposable
        {
            var a = new AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>>(b.info);
            return new ScopedAsyncAtomicLruBuilder<K, V>(a);
        }

        public static ScopedAsyncAtomicLruBuilder<K, V> WithScopedValues<K, V>(this AsyncAtomicLruBuilder<K, V> b) where V : IDisposable
        {
            var a = new AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>>(b.info);
            return new ScopedAsyncAtomicLruBuilder<K, V>(a);
        }

        public static ScopedAsyncAtomicLruBuilder<K, V> WithAtomicCreate<K, V>(this ScopedAsyncLruBuilder<K, V> b) where V : IDisposable
        {
            var a = new AsyncConcurrentLruBuilder<K, ScopedAsyncAtomicFactory<K, V>>(b.info);
            return new ScopedAsyncAtomicLruBuilder<K, V>(a);
        }
    }
}
