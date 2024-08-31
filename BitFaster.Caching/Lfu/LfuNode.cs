#nullable disable
using System.Runtime.CompilerServices;
using System.Threading;

namespace BitFaster.Caching.Lfu
{
    internal class LfuNode<K, V>
        where K : notnull
    {
        private V data;

        internal LfuNodeList<K, V> list;
        internal LfuNode<K, V> next;
        internal LfuNode<K, V> prev;

        private volatile bool wasRemoved;
        private volatile bool wasDeleted;

        // only used when V is a non-atomic value type to prevent torn reads
        private int sequence;

        public LfuNode(K k, V v)
        {
            this.Key = k;
            this.data = v;
        }

        public readonly K Key;

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

    internal enum Position : short
    {
        Window,
        Probation,
        Protected,
    }

    internal sealed class AccessOrderNode<K, V> : LfuNode<K, V>
        where K : notnull
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
