using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lfu.Builder
{
    public class AtomicScopedConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, AtomicScopedConcurrentLfuBuilder<K, V>, IScopedCache<K, V>> where V : IDisposable
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
