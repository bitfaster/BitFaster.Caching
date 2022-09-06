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

        private MpscBoundedBuffer<T>[] buffers;

        public StripedMpscBuffer(int stripeCount, int bufferSize)
            : this(new StripedBufferSize(bufferSize, stripeCount))
        { 
        }

        public StripedMpscBuffer(StripedBufferSize bufferSize)
        {
            buffers = new MpscBoundedBuffer<T>[bufferSize.StripeCount];

            for (var i = 0; i < bufferSize.StripeCount; i++)
            {
                buffers[i] = new MpscBoundedBuffer<T>(bufferSize.BufferSize);
            }
        }

        public int Count => buffers.Sum(b => b.Count);

        public int Capacity => buffers.Length * buffers[0].Capacity;

        public int DrainTo(T[] outputBuffer)
        {
            var count = 0;

            for (var i = 0; i < buffers.Length; i++)
            {
                if (count == outputBuffer.Length)
                {
                    break;
                }

                var segment = new ArraySegment<T>(outputBuffer, count, outputBuffer.Length - count);
                count += buffers[i].DrainTo(segment);
            }

            return count;
        }

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

        public void Clear()
        {
            for (var i = 0; i < buffers.Length; i++)
            {
                buffers[i].Clear();
            }
        }
    }
}
