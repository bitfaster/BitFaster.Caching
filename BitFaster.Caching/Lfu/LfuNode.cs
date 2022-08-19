using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lfu
{
    internal class LfuNode<K, V>
    {
        internal LfuNodeList<K, V> list;
        internal LfuNode<K, V> next;
        internal LfuNode<K, V> prev;

        private volatile bool wasRemoved;

        public LfuNode(K k, V v)
        {
            this.Key = k;
            this.Value = v;
        }

        public LfuNode(LfuNodeList<K, V> list, K k, V v)
        {
            this.list = list;
            this.Key = k;
            this.Value = v;
        }

        public readonly K Key;

        public V Value { get; set; }

        public Position Position { get; set; }

        public bool WasRemoved
        {
            get => this.wasRemoved;
            set => this.wasRemoved = value;
        }

        public LfuNode<K, V> Next
        {
            get { return next == null || next == list.head ? null : next; }
        }

        internal void Invalidate()
        {
            list = null;
            next = null;
            prev = null;
        }
    }

    public enum Position
    {
        Window,
        Probation,
        Protected,
    }
}
