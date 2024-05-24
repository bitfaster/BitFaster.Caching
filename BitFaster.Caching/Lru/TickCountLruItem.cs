using System;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents an LRU item that also stores tick count.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    public class TickCountLruItem<K, V> : LruItem<K, V>
        where K : notnull
    {
        /// <summary>
        /// Initializes a new instance of the TickCountLruItem class with the specified key and value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public TickCountLruItem(K key, V value)
            : base(key, value)
        {
            this.TickCount = Environment.TickCount;
        }

        /// <summary>
        /// Gets or sets the tick count.
        /// </summary>
        public int TickCount { get; set; }
    }
}
