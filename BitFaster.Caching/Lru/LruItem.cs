
using System;
using System.Collections.Generic;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents an LRU item.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    public class LruItem<K, V> : IEquatable<LruItem<K, V>?>
    {
        private volatile bool wasAccessed;
        private volatile bool wasRemoved;

        /// <summary>
        /// Initializes a new instance of the LruItem class with the specified key and value.
        /// </summary>
        /// <param name="k">The key.</param>
        /// <param name="v">The value.</param>
        public LruItem(K k, V v)
        {
            this.Key = k;
            this.Value = v;
        }

        /// <summary>
        /// Gets the key.
        /// </summary>
        public readonly K Key;

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public V Value { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the item was accessed.
        /// </summary>
        public bool WasAccessed
        {
            get => this.wasAccessed;
            set => this.wasAccessed = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the item was removed.
        /// </summary>
        public bool WasRemoved
        {
            get => this.wasRemoved;
            set => this.wasRemoved = value;
        }

        ///<inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as LruItem<K, V>);
        }

        ///<inheritdoc/>
        public bool Equals(LruItem<K, V>? other)
        {
            return other is not null &&
                   EqualityComparer<K>.Default.Equals(Key, other.Key) &&
                   EqualityComparer<V>.Default.Equals(Value, other.Value);
        }

        ///<inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(Key, Value);
        }
    }
}
