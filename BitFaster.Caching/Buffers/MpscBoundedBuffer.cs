using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BitFaster.Caching.Buffers
{
    /// <summary>
    /// Provides a multi-producer, single-consumer thread-safe ring buffer. When the buffer is full,
    /// TryAdd fails and returns false. When the buffer is empty, TryTake fails and returns false.
    /// </summary>
    /// Based on the BoundedBuffer class in the Caffeine library by ben.manes@gmail.com (Ben Manes).
    [DebuggerDisplay("Count = {Count}/{Capacity}")]
    public sealed class MpscBoundedBuffer<T> where T : class
    {
        private readonly T?[] buffer;
        private readonly int mask;
        private PaddedHeadAndTail headAndTail; // mutable struct, don't mark readonly

        /// <summary>
        /// Initializes a new instance of the MpscBoundedBuffer class with the specified bounded capacity.
        /// </summary>
        /// <param name="boundedLength">The bounded length.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public MpscBoundedBuffer(int boundedLength)
        {
            if (boundedLength < 0)
                Throw.ArgOutOfRange(nameof(boundedLength));

            // must be power of 2 to use & slotsMask instead of %
            boundedLength = BitOps.CeilingPowerOfTwo(boundedLength);

            buffer = new T[boundedLength];
            mask = boundedLength - 1;
        }

        /// <summary>
        /// The bounded capacity.
        /// </summary>
        public int Capacity => buffer.Length;

        /// <summary>
        /// Gets the number of items contained in the buffer.
        /// </summary>
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

        /// <summary>
        /// Tries to add the specified item.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        /// <returns>A BufferStatus value indicating whether the operation succeeded.</returns>
        /// <remarks>
        /// Thread safe.
        /// </remarks>
        public BufferStatus TryAdd(T item)
        {
            int head = Volatile.Read(ref headAndTail.Head);
            int tail = headAndTail.Tail;
            int size = tail - head;

            if (size >= buffer.Length)
            {
                return BufferStatus.Full;
            }

            if (Interlocked.CompareExchange(ref this.headAndTail.Tail, tail + 1, tail) == tail)
            {
                int index = tail & mask;
                Volatile.Write(ref buffer[index], item);

                return BufferStatus.Success;
            }

            return BufferStatus.Contended;
        }


        /// <summary>
        /// Tries to remove an item.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        /// <returns>A BufferStatus value indicating whether the operation succeeded.</returns>
        /// <remarks>
        /// Thread safe for single try take/drain + multiple try add.
        /// </remarks>
        public BufferStatus TryTake(out T? item)
        {
            int head = Volatile.Read(ref headAndTail.Head);
            int tail = headAndTail.Tail;
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

            buffer[index] = null;
            Volatile.Write(ref this.headAndTail.Head, ++head);
            return BufferStatus.Success;
        }

        // On NETSTANDARD2_0 all code paths are internally based on ArraySegment<T>.
        // After NETSTANDARD2_0, all code paths are internally based on Span<T>.
#if NETSTANDARD2_0
        /// <summary>
        /// Drains the buffer into the specified array segment.
        /// </summary>
        /// <param name="output">The output buffer</param>
        /// <returns>The number of items written to the output buffer.</returns>
        /// <remarks>
        /// Thread safe for single try take/drain + multiple try add.
        /// </remarks>
        public int DrainTo(ArraySegment<T> output)
#else
        /// <summary>
        /// Drains the buffer into the specified array segment.
        /// </summary>
        /// <param name="output">The output buffer</param>
        /// <returns>The number of items written to the output buffer.</returns>
        /// <remarks>
        /// Thread safe for single try take/drain + multiple try add.
        /// </remarks>
        public int DrainTo(ArraySegment<T> output)
        { 
            return DrainTo(output.AsSpan());
        }

        /// <summary>
        /// Drains the buffer into the specified span.
        /// </summary>
        /// <param name="output">The output buffer</param>
        /// <returns>The number of items written to the output buffer.</returns>
        /// <remarks>
        /// Thread safe for single try take/drain + multiple try add.
        /// </remarks>
        public int DrainTo(Span<T> output)
#endif
        {
            return DrainToImpl(output);
        }

        // use an outer wrapper method to force the JIT to inline the inner adaptor methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_0
        private int DrainToImpl(ArraySegment<T> output)
#else
        private int DrainToImpl(Span<T> output)
#endif
        {
            int head = Volatile.Read(ref headAndTail.Head);
            int tail = headAndTail.Tail;
            int size = tail - head;

            if (size == 0)
            {
                return 0;
            }

            var localBuffer = buffer.AsSpanOrArray();

            int outCount = 0;

            do
            {
                int index = head & mask;

                T? item = Volatile.Read(ref localBuffer[index]);

                if (item == null)
                {
                    // not published yet
                    break;
                }

                localBuffer[index] = null;
                Write(output, outCount++, item);
                head++;
            }
            while (head != tail && outCount < Length(output));

            this.headAndTail.Head = head;

            return outCount;
        }

#if NETSTANDARD2_0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write(ArraySegment<T> output, int index, T item)
        {
            output.Array[output.Offset + index] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Length(ArraySegment<T> output)
        {
            return output.Count;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write(Span<T> output, int index, T item)
        {
            output[index] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Length(Span<T> output)
        {
            return output.Length;
        }
#endif

        /// <summary>
        /// Removes all values from the buffer.
        /// </summary>
        /// <remarks>
        /// Clear must be called from the single consumer thread.
        /// </remarks>
        public void Clear()
        {
            while (TryTake(out _) != BufferStatus.Empty)
            {
            }
        }
    }
}
