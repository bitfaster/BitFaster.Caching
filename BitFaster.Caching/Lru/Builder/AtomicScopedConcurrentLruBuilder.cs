using System;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLru with scoped values.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    public class AtomicScopedConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, AtomicScopedConcurrentLruBuilder<K, V>, IScopedCache<K, V>> where V : IDisposable
    {
        private readonly ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>> inner;

        internal AtomicScopedConcurrentLruBuilder(ConcurrentLruBuilder<K, ScopedAtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override IScopedCache<K, V> Build()
        {
            var level1 = inner.Build() as ICache<K, ScopedAtomicFactory<K, V>>;
            return new AtomicFactoryScopedCache<K, V>(level1);
        }
    }
}
