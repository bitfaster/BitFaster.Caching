using System;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lfu.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLfu with scoped values.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    public sealed class AtomicScopedConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, AtomicScopedConcurrentLfuBuilder<K, V>, IScopedCache<K, V>>
        where K : notnull
        where V : IDisposable
    {
        private readonly ConcurrentLfuBuilder<K, ScopedAtomicFactory<K, V>> inner;

        internal AtomicScopedConcurrentLfuBuilder(ConcurrentLfuBuilder<K, ScopedAtomicFactory<K, V>> inner)
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
