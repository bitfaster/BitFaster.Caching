using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lfu.Builder
{
    public sealed class AsyncConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, AsyncConcurrentLfuBuilder<K, V>, IAsyncCache<K, V>>
    {
        internal AsyncConcurrentLfuBuilder(LfuInfo<K> info)
            : base(info)
        {
        }

        ///<inheritdoc/>
        public override IAsyncCache<K, V> Build()
        {
            return new ConcurrentLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, info.BufferConfiguration);
        }
    }
}
