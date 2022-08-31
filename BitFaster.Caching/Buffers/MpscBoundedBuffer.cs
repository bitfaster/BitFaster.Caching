using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Buffers
{
    /// <summary>
    /// Provides a multi-producer, single-consumer thread-safe ring buffer. When the buffer is full,
    /// TryAdd fails and returns false. When the buffer is empty, TryTake fails and returns false.
    /// </summary>
    /// <remarks>
    /// Based on BoundedBuffer by Ben Manes.
    /// https://github.com/ben-manes/caffeine/blob/master/caffeine/src/main/java/com/github/benmanes/caffeine/cache/BoundedBuffer.java
    /// </remarks>
    [DebuggerDisplay("Count = {Count}/{Capacity}")]
    public class MpscBoundedBuffer<T> where T : class
    {
        private T[] buffer;
        private readonly int mask;
        private PaddedHeadAndTail headAndTail; // mutable struct, don't mark readonly

        public MpscBoundedBuffer(int boundedLength)
        {
            if (boundedLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boundedLength));
            }

            // must be power of 2 to use & slotsMask instead of %
            boundedLength = BitOps.CeilingPowerOfTwo(boundedLength);

            buffer = new T[boundedLength];
            mask = boundedLength - 1;
        }

        public int Capacity => buffer.Length;

        public int Count
        {
            get
            {
                var spinner = new SpinWait();
                while (true)
                {
                    var headNow = Volatile.Read(ref headAndTail.Head);
                    var tailNow = Volatile.Read(ref headAndTail.Tail);

                    if (headNow == Volatile.Read(ref headAndTail.Head) &&
                        tailNow == Volatile.Read(ref headAndTail.Tail))
                    {
                        return GetCount(headNow, tailNow);
                    }

                    spinner.SpinOnce();
                }
            }
        }

        private int GetCount(int head, int tail)
        {
            if (head != tail)
            {
                head &= mask;
                tail &= mask;

                return head < tail ? tail - head : buffer.Length - head + tail;
            }
            return 0;
        }

        // thread safe
        public BufferStatus TryAdd(T item)
        {
            int head = Volatile.Read(ref headAndTail.Head);
            int tail = Volatile.Read(ref headAndTail.Tail);
            int size = tail - head;

            if (size >= buffer.Length)
            {
                return BufferStatus.Full;
            }

            if (Interlocked.CompareExchange(ref this.headAndTail.Tail, tail + 1, tail) == tail)
            {
                int index = (int)(tail & mask);
                Volatile.Write(ref buffer[index], item);

                return BufferStatus.Success;
            }

            return BufferStatus.Contended;
        }

        // thread safe for single try take/drain + multiple try add
        public BufferStatus TryTake(out T item)
        {
            int head = Volatile.Read(ref headAndTail.Head);
            int tail = Volatile.Read(ref headAndTail.Tail);
            int size = tail - head;

            if (size == 0)
            {
                item = default;
                return BufferStatus.Empty;
            }

            int index = head & mask;

            item = Volatile.Read(ref buffer[index]);

            if (item == null)
            {
                // not published yet
                return BufferStatus.Contended;
            }

            Volatile.Write(ref buffer[index], null);
            Volatile.Write(ref this.headAndTail.Head, ++head);
            return BufferStatus.Success;
        }

        // thread safe for single try take/drain + multiple try add
        public int DrainTo(ArraySegment<T> output)
        {
            int head = Volatile.Read(ref headAndTail.Head);
            int tail = Volatile.Read(ref headAndTail.Tail);
            int size = tail - head;

            if (size == 0)
            {
                return 0;
            }

            int outCount = 0;

            do
            {
                int index = head & mask;

                T item = Volatile.Read(ref buffer[index]);

                if (item == null)
                {
                    // not published yet
                    break;
                }

                Volatile.Write(ref buffer[index], null);
                output.Array[output.Offset + outCount++] = item;
                head++;
            }
            while (head != tail && outCount < output.Count);

            Volatile.Write(ref this.headAndTail.Head, head);

            return outCount;
        }

        // Not thread safe
        public void Clear()
        {
            buffer = new T[buffer.Length];
            headAndTail = new PaddedHeadAndTail();
        }
    }
}
