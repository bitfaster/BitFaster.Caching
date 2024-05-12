﻿#nullable disable
namespace BitFaster.Caching.Lfu
{
    internal class LfuNode<K, V>
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

    internal sealed class TimeOrderNode<K, V> : LfuNode<K, V>
        where K : notnull
    {
        TimeOrderNode<K, V> prevTime;
        TimeOrderNode<K, V> nextTime;

        private Duration timeToExpire;

        public TimeOrderNode(K k, V v) : base(k, v)
        {
        }

        public Duration TimeToExpire 
        { 
            get => timeToExpire;
            set => timeToExpire = value;
        }

        public static TimeOrderNode<K, V> CreateSentinel()
        {
            var s = new TimeOrderNode<K, V>(default, default);
            s.nextTime = s.prevTime = s;
            return s;
        }

        public TimeOrderNode<K, V> GetPreviousInTimeOrder()
        {
            return prevTime;
        }

        public long GetTimestamp()
        {
            return timeToExpire.raw;
        }

        public void SetPreviousInTimeOrder(TimeOrderNode<K, V> prev)
        {
            this.prevTime = prev;
        }

        public TimeOrderNode<K, V> GetNextInTimeOrder()
        {
            return nextTime;
        }

        public void SetNextInTimeOrder(TimeOrderNode<K, V> next)
        {
            this.nextTime = next;
        }
    }
}
