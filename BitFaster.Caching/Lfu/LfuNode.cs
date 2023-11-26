namespace BitFaster.Caching.Lfu
{
    public class LfuNode<K, V>
    {
        internal LfuNodeList<K, V> list;
        internal LfuNode<K, V> next;
        internal LfuNode<K, V> prev;

        private volatile bool wasRemoved;
        private volatile bool wasDeleted;

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

    public enum Position
    {
        Window,
        Probation,
        Protected,
    }

    // existing scheme is purely access order
    public sealed class AccessOrderNode<K, V> : LfuNode<K, V>
    {
        public AccessOrderNode(K k, V v) : base(k, v)
        {
        }
    }

    // expire after access requires time to expire
    internal sealed class AccessOrderExpiringNode<K, V> : LfuNode<K, V>
    {
        private Duration timeToExpire;

        public AccessOrderExpiringNode(K k, V v) : base(k, v)
        {
        }
    }

    // both ExpireAfter and ExpireAfterWrite require
    // 1. Duration
    // 2. Doubly linked list
    public sealed class TimeOrderNode<K, V> : LfuNode<K, V>
    {
        TimeOrderNode<K, V> prevV;
        TimeOrderNode<K, V> nextV;

        private Duration timeToExpire;

        public TimeOrderNode(K k, V v) : base(k, v)
        {
        }

        public static TimeOrderNode<K, V> CreateSentinel()
        {
            var s = new TimeOrderNode<K, V>(default, default);
            s.nextV = s.prevV = s;
            return s;
        }

        public TimeOrderNode<K, V> getPreviousInVariableOrder()
        {
            return prevV;
        }

        public long getVariableTime()
        {
            return timeToExpire.raw;
        }

        //override
        public void setPreviousInVariableOrder(TimeOrderNode<K, V> prev)
        {
            this.prevV = prev;
        }
        //override
        public TimeOrderNode<K, V> getNextInVariableOrder()
        {
            return nextV;
        }

        // override
        public void setNextInVariableOrder(TimeOrderNode<K, V> next)
        {
            this.nextV = next;
        }
    }
}
