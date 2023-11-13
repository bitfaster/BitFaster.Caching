using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lru
{
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
