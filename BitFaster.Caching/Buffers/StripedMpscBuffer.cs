using System;
using System.Diagnostics;
using System.Linq;

namespace BitFaster.Caching.Buffers
{
    /// <summary>
    /// Provides a striped bounded buffer. Add operations use thread ID to index into
    /// the underlying array of buffers, and if TryAdd is contended the thread ID is 
    /// rehashed to select a different buffer to retry up to 3 times. Using this approach
    /// writes scale linearly with number of concurrent threads.
    /// </summary>
    [DebuggerDisplay("Count = {Count}/{Capacity}")]
    public sealed class StripedMpscBuffer<T> where T : class
    {
        const int MaxAttempts = 3;

        private readonly MpscBoundedBuffer<T>[] buffers;

        /// <summary>
        /// Initializes a new instance of the StripedMpscBuffer class with the specified stripe count and buffer size.
        /// </summary>
        /// <param name="stripeCount">The stripe count.</param>
        /// <param name="bufferSize">The buffer size.</param>
        public StripedMpscBuffer(int stripeCount, int bufferSize)
        { 
            buffers = new MpscBoundedBuffer<T>[stripeCount];

            for (var i = 0; i < stripeCount; i++)
            {
                buffers[i] = new MpscBoundedBuffer<T>(bufferSize);
            }
        }

        /// <summary>
        /// Gets the number of items contained in the buffer.
        /// </summary>
        public int Count => buffers.Sum(b => b.Count);

        /// <summary>
        /// The bounded capacity.
        /// </summary>
        public int Capacity => buffers.Length * buffers[0].Capacity;

        /// <summary>
        /// Drains the buffer into the specified array.
        /// </summary>
        /// <param name="outputBuffer">The output buffer</param>
        /// <returns>The number of items written to the output buffer.</returns>
        /// <remarks>
        /// Thread safe for single try take/drain + multiple try add.
        /// </remarks>
#if NETSTANDARD2_0
        public int DrainTo(T[] outputBuffer)
#else
        public int DrainTo(T[] outputBuffer)
        { 
            return DrainTo(outputBuffer.AsSpan());
        }

        /// <summary>
        /// Drains the buffer into the specified span.
        /// </summary>
        /// <param name="outputBuffer">The output buffer</param>
        /// <returns>The number of items written to the output buffer.</returns>
        /// <remarks>
        /// Thread safe for single try take/drain + multiple try add.
        /// </remarks>
        public int DrainTo(Span<T> outputBuffer)
#endif
        {
            var count = 0;

            for (var i = 0; i < buffers.Length; i++)
            {
                if (count == outputBuffer.Length)
                {
                    break;
                }

                var segment = outputBuffer.Slice(count, outputBuffer.Length - count);

                count += buffers[i].DrainTo(segment);
            }

            return count;
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
            var z = BitOps.Mix64((ulong)Environment.CurrentManagedThreadId);
            var inc = (int)(z >> 32) | 1;
            var h = (int)z;

            var mask = buffers.Length - 1;

            var result = BufferStatus.Empty;

            for (var i = 0; i < MaxAttempts; i++)
            {
                result = buffers[h & mask].TryAdd(item);

                if (result == BufferStatus.Success)
                {
                    break;
                }

                h += inc;
            }

            return result;
        }

        /// <summary>
        /// Removes all values from the buffer.
        /// </summary>
        /// <remarks>
        /// Not thread safe.
        /// </remarks>
        public void Clear()
        {
            for (var i = 0; i < buffers.Length; i++)
            {
                buffers[i].Clear();
            }
        }
    }
}
