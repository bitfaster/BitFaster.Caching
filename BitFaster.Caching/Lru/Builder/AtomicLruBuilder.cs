using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;

namespace BitFaster.Caching.Lru.Builder
{
    public class AtomicLruBuilder<K, V> : LruBuilderBase<K, V, AtomicLruBuilder<K, V>, ICache<K, V>>
    {
        private readonly ConcurrentLruBuilder<K, AtomicFactory<K, V>> inner;

        internal AtomicLruBuilder(ConcurrentLruBuilder<K, AtomicFactory<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override ICache<K, V> Build()
        {
            var level1 = inner.Build();
            return new AtomicFactoryCache<K, V>(level1);
        }
    }

    public class AsyncAtomicLruBuilder<K, V> : LruBuilderBase<K, V, AsyncAtomicLruBuilder<K, V>, IAsyncCache<K, V>>
    {
        private readonly ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>> inner;

        internal AsyncAtomicLruBuilder(ConcurrentLruBuilder<K, AsyncAtomicFactory<K, V>> inner)
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
