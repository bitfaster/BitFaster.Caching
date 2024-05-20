namespace SeqLock
{
    public class LruItem<K, V>
    {
        private volatile bool wasAccessed;
        private volatile bool wasRemoved;

        private int sequence;

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

        public V Read()
        { 
            //return this.Value;

            var spin = new SpinWait();
            while (true)
            { 
                var start = Volatile.Read(ref this.sequence);

                if ((start & 1) == 1) 
                {
                    // A write is in progress. Back off and keep spinning.
                    spin.SpinOnce();
                    continue;
                }

                V copy = this.Value;

                var end = Volatile.Read(ref this.sequence);
                if (start == end)
                { 
                    return copy;    
                }
            }
        }

        public void Write(V value)
        { 
            lock (this) 
            {
                Interlocked.Increment(ref sequence);

                this.Value = value;

                Interlocked.Increment(ref sequence);
            }
        }
    }
}
