
using BitFaster.Caching.Lfu.Builder;

namespace BitFaster.Caching.Lfu
{
    public sealed class ConcurrentLfuBuilder<K, V> : LfuBuilderBase<K, V, ConcurrentLfuBuilder<K, V>, ICache<K, V>>
    {
        /// <summary>
        /// Creates a ConcurrentLfuBuilder. Chain method calls onto ConcurrentLfuBuilder to configure the cache then call Build to create a cache instance.
        /// </summary>
        public ConcurrentLfuBuilder()
            : base(new LfuInfo<K>())
        {
        }

        internal ConcurrentLfuBuilder(LfuInfo<K> info)
            : base(info)
        {
        }

        ///<inheritdoc/>
        public override ICache<K, V> Build()
        {
            return new ConcurrentLfu<K, V>(info.ConcurrencyLevel, info.Capacity, info.Scheduler, info.KeyComparer, info.BufferConfiguration);
        }
    }
}
