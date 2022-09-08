
namespace BitFaster.Caching
{
    /// <summary>
    /// Extension methods for the SingletonCache class.
    /// </summary>
    public static class SingletonCacheExtensions
    {
        /// <summary>
        /// Acquire a singleton value for the specified key. The lifetime guarantees the value is alive and is a singleton 
        /// for the given key until the lifetime is disposed.
        /// </summary>
        /// <param name="cache">The cache to use.</param>
        /// <param name="key">The key of the item</param>
        /// <returns>A value lifetime</returns>
        public static Lifetime<TValue> Acquire<TKey, TValue>(this SingletonCache<TKey, TValue> cache, TKey key) 
            where TValue : new()
        {
            return cache.Acquire(key, _ => new TValue());
        }
    }
}
