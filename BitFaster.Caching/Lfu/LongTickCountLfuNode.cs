
namespace BitFaster.Caching.Lfu
{
    internal class LongTickCountLfuNode<K, V> : LfuNode<K, V>
    {
        public LongTickCountLfuNode(K k, V v, long tickCount) 
            : base(k, v)
        {
            this.TickCount = tickCount;
        }

        /// <summary>
        /// Gets or sets the tick count.
        /// </summary>
        public long TickCount { get; set; }
    }
}
