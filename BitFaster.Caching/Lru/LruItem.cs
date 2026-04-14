
using System.Runtime.CompilerServices;
using System.Threading;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Represents an LRU item.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    public class LruItem<K, V>
        where K : notnull
    {
        private V data;

        private bool wasAccessed;
        private bool wasRemoved;

        // only used when V is a non-atomic value type to prevent torn reads
        private int sequence;

        /// <summary>
        /// Initializes a new instance of the LruItem class with the specified key and value.
        /// </summary>
        /// <param name="k">The key.</param>
        /// <param name="v">The value.</param>
        public LruItem(K k, V v)
        {
            this.Key = k;
            this.data = v;
        }

        /// <summary>
        /// Gets the key.
        /// </summary>
        public readonly K Key;

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public V Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TypeProps<V>.IsWriteAtomic)
                {
                    return data;
                }
                else
                {
                    return SeqLockRead();
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (TypeProps<V>.IsWriteAtomic)
                {
                    data = value;
                }
                else
                {
                    SeqLockWrite(value);
                }
            }
        }

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

        /// <summary>
        /// Marks the item as accessed, if it was not already accessed.
        /// </summary>
        public void MarkAccessed()
        {
            if (!this.wasAccessed)
            {
                this.wasAccessed = true;
            }
        }

        internal V SeqLockRead()
        {
            var spin = new SpinWait();
            while (true)
            {
                var start = Volatile.Read(ref this.sequence);

                if ((start & 1) == 1)
                {
                    // A write is in progress, spin.
                    spin.SpinOnce();
                    continue;
                }

                V copy = this.data;

                var end = Volatile.Read(ref this.sequence);
                if (start == end)
                {
                    return copy;
                }
            }
        }

        // Note: LruItem should be locked while invoking this method. Multiple writer threads are not supported.
        internal void SeqLockWrite(V value)
        {
            Interlocked.Increment(ref sequence);

            this.data = value;

            Interlocked.Increment(ref sequence);
        }
    }
}
