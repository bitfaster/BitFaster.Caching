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
            switch (info)
            {
                case LruInfo<K> i when i.WithMetrics && !i.Expiration.HasValue:
                    return new ConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer);
                case LruInfo<K> i when i.WithMetrics && i.Expiration.HasValue:
                    return new ConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.Expiration.Value);
                case LruInfo<K> i when i.Expiration.HasValue:
                    return new FastConcurrentTLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer, info.Expiration.Value);
                default:
                    return new FastConcurrentLru<K, V>(info.ConcurrencyLevel, info.Capacity, info.KeyComparer);
            }
        }
    }
}
