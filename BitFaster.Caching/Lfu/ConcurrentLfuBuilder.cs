using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Lfu.Builder;
using BitFaster.Caching.Lru.Builder;

namespace BitFaster.Caching.Lfu
{
    public sealed class ConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, ConcurrentLfuBuilder<K, V>, ICache<K, V>>
    {
        /// <summary>
        /// Creates a ConcurrentLruBuilder. Chain method calls onto ConcurrentLruBuilder to configure the cache then call Build to create a cache instance.
        /// </summary>
        public ConcurrentLfuBuilder()
            : base(new LfuInfo<K>())
        {
        }

        internal ConcurrentLfuBuilder(LfuInfo<K> info)
            : base(info)
        {
        }

        public override ICache<K, V> Build()
        {
            // TODO: key comparer
            return new ConcurrentLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer);
        }
    }
}
