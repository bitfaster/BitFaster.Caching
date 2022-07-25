using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;

namespace BitFaster.Caching.Lru.Builder
{
    public class AtomicAsyncConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, AtomicAsyncConcurrentLruBuilder<K, V>, IAsyncCache<K, V>>
    {
        private readonly ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>> inner;

        internal AtomicAsyncConcurrentLruBuilder(ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>> inner)
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
