using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;

namespace BitFaster.Caching.Lru.Builder
{
    public class IdempotentAsyncLruBuilder<K, V> : LruBuilderBase<K, V, IdempotentAsyncLruBuilder<K, V>, ICache<K, V>>
    {
        private readonly ConcurrentLruBuilder<K, AsyncIdempotent<K, V>> inner;

        internal IdempotentAsyncLruBuilder(ConcurrentLruBuilder<K, AsyncIdempotent<K, V>> inner)
            : base(inner.info)
        {
            this.inner = inner;
        }

        public override ICache<K, V> Build()
        {
            var innerCache = inner.Build();

            return new IdempotentAsyncCache<K, V>(innerCache);
        }
    }
}
