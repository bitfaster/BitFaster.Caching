using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru.Builder;

namespace BitFaster.Caching.Lru
{
    public class ConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, ConcurrentLruBuilder<K, V>, ICache<K, V>>
    {
        public ConcurrentLruBuilder()
            : base(new LruInfo<K>())
        {
        }

        internal ConcurrentLruBuilder(LruInfo<K> info)
            : base(info)
        {
        }

        public override ICache<K, V> Build()
        {
            if (info.expiration.HasValue)
            {
                return info.withMetrics ?
                    new ConcurrentTLru<K, V>(info.concurrencyLevel, info.capacity, info.comparer, info.expiration.Value)
                    : new FastConcurrentTLru<K, V>(info.concurrencyLevel, info.capacity, info.comparer, info.expiration.Value) as ICache<K, V>;
            }

            return info.withMetrics ?
                new ConcurrentLru<K, V>(info.concurrencyLevel, info.capacity, info.comparer)
                : new FastConcurrentLru<K, V>(info.concurrencyLevel, info.capacity, info.comparer) as ICache<K, V>;
        }
    }
}
