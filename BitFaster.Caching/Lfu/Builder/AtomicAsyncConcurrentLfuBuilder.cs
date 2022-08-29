using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lfu.Builder
{
    public class AtomicAsyncConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, AtomicAsyncConcurrentLfuBuilder<K, V>, IAsyncCache<K, V>>
    {
        private readonly ConcurrentLfuBuilder<K, AsyncAtomicFactory<K, V>> inner;

        internal AtomicAsyncConcurrentLfuBuilder(ConcurrentLfuBuilder<K, AsyncAtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override IAsyncCache<K, V> Build()
        {
            var level1 = inner.Build();
            return new AtomicFactoryAsyncCache<K, V>(level1);
        }
    }
}
