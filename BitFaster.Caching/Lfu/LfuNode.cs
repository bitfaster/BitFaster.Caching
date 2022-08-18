using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lfu
{
    internal class LfuNode<K, V>
    {
        private volatile bool wasRemoved;

        public LfuNode(K k, V v)
        {
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
    }

    public enum Position
    {
        Window,
        Probation,
        Protected,
    }
}
