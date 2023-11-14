
namespace BitFaster.Caching.Lru.Builder
{
    /// <summary>
    /// A builder for creating a ConcurrentLru.
    /// </summary>
    /// <typeparam name="K">The type of the cache key.</typeparam>
    /// <typeparam name="V">The type of the cache value.</typeparam>
    public sealed class AsyncConcurrentLruBuilder<K, V> : LruBuilderBase<K, V, AsyncConcurrentLruBuilder<K, V>, IAsyncCache<K, V>>
    {
        internal AsyncConcurrentLruBuilder(LruInfo<K> info)
            : base(info)
        {
        }

        ///<inheritdoc/>
        public override IAsyncCache<K, V> Build()
        {
            return ConcurrentLru.Create<K, V>(this.info) as IAsyncCache<K, V>;
        }
    }
}
