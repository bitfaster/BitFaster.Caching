using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru.Builder
{
    public class AtomicLruBuilder<K, V> : LruBuilderBase<K, V, AtomicLruBuilder<K, V>, ICache<K, V>>
    {
        private readonly ConcurrentLruBuilder<K, AsyncAtomic<K, V>> inner;

        internal AtomicLruBuilder(ConcurrentLruBuilder<K, AsyncAtomic<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override ICache<K, V> Build()
        {
            var innerCache = inner.Build();

            return new AtomicCacheDecorator<K, V>(innerCache);
        }
    }
}
