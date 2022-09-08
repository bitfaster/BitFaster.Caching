using System;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents an LRU item that also stores the item time stamp.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    public class TimeStampedLruItem<K, V> : LruItem<K, V>
    {
        /// <summary>
        /// Initializes a new instance of the TimeStampedLruItem class with the specified key and value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public TimeStampedLruItem(K key, V value)
            : base(key, value)
        {
            this.TimeStamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets or sets the time stamp.
        /// </summary>
        public DateTime TimeStamp { get; set; }
    }
}
