using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lfu.Builder
{
    public class AtomicConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, AtomicConcurrentLfuBuilder<K, V>, ICache<K, V>>
    {
        private readonly ConcurrentLfuBuilder<K, AtomicFactory<K, V>> inner;

        internal AtomicConcurrentLfuBuilder(ConcurrentLfuBuilder<K, AtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        ///<inheritdoc/>
        public override ICache<K, V> Build()
        {
            var level1 = inner.Build();
            return new AtomicFactoryCache<K, V>(level1);
        }
    }
}
