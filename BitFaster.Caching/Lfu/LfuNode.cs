namespace BitFaster.Caching.Lfu
{
    internal class LfuNode<K, V>
    {
        internal LfuNodeList<K, V> list;
        internal LfuNode<K, V> next;
        internal LfuNode<K, V> prev;

        private bool wasRemoved;
        private bool wasDeleted;

        public LfuNode(K k, V v)
        {
            this.Key = k;
            this.Value = v;
        }

        public readonly K Key;

        public V Value { get; set; }

        public Position Position { get; set; }

        /// <summary>
        /// Node was removed from the dictionary, but is still present in the LRU lists.
        /// </summary>
        public bool WasRemoved
        {
            get => this.wasRemoved;
            set => this.wasRemoved = value;
        }

        /// <summary>
        /// Node has been removed both from the dictionary and the LRU lists.
        /// </summary>
        public bool WasDeleted
        {
            get => this.wasDeleted;
            set => this.wasDeleted = value;
        }

        public LfuNode<K, V> Next
        {
            get { return next == null || next == list.head ? null : next; }
        }

        public LfuNode<K, V> Previous
        {
            get { return prev == null || this == list.head ? null : prev; }
        }

        internal void Invalidate()
        {
            list = null;
            next = null;
            prev = null;
        }
    }

    internal enum Position
    {
        Window,
        Probation,
        Protected,
    }

    internal sealed class AccessOrderNode<K, V> : LfuNode<K, V>
    {
        public AccessOrderNode(K k, V v) : base(k, v)
        {
        }
    }
}
