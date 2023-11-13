using System;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// A marker interface for discrete expiry policies.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public interface IDiscreteItemPolicy<K, V> : IItemPolicy<K, V, LongTickCountLruItem<K, V>>
    {
        /// <summary>
        /// Convert ticks to a TimeSpan.
        /// </summary>
        /// <param name="ticks">The number of ticks to convert.</param>
        /// <returns>Ticks converted to a TimeSpan</returns>
        TimeSpan ConvertTicks(long ticks);
    }
}
