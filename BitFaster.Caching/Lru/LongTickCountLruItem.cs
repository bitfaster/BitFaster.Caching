
using System;
using System.Collections.Generic;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents an LRU item that also stores tick count.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    public class LongTickCountLruItem<K, V> : LruItem<K, V>, IEquatable<LongTickCountLruItem<K, V>?>
    {
        /// <summary>
        /// Initializes a new instance of the LongTickCountLruItem class with the specified key and value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tickCount">The tick count.</param>
        public LongTickCountLruItem(K key, V value, long tickCount)
            : base(key, value)
        {
            this.TickCount = tickCount;
        }

        /// <summary>
        /// Gets or sets the tick count.
        /// </summary>
        public long TickCount { get; set; }

        ///<inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as LongTickCountLruItem<K, V>);
        }

        ///<inheritdoc/>
        public bool Equals(LongTickCountLruItem<K, V>? other)
        {
             return ReferenceEquals(this, other);
        }

        ///<inheritdoc/>
        public override int GetHashCode()
        {
            return Hash(Key, Value);
        }
    }
}
