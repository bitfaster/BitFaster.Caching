using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;

namespace BitFaster.Caching.Lru.Builder
{
    public class AtomicFactoryAsyncLruBuilder<K, V> : LruBuilderBase<K, V, AtomicFactoryAsyncLruBuilder<K, V>, ICache<K, V>>
    {
        private readonly ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>> inner;

        internal AtomicFactoryAsyncLruBuilder(ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override ICache<K, V> Build()
        {
            var innerCache = inner.Build();

            return new AtomicFactoryAsyncCache<K, V>(innerCache);
        }
    }
}
