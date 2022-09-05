using System;

namespace BitFaster.Caching.Buffers
{
    /// <summary>
    /// Provides a striped bounded buffer. Add operations use thread ID to index into
    /// the underlying array of buffers, and if TryAdd is contended the thread ID is 
    /// rehashed to select a different buffer to retry up to 3 times. Using this approach
    /// writes scale linearly with number of concurrent threads.
    /// </summary>
    public sealed class StripedMpmcBuffer<T>
    {
        const int MaxAttempts = 3;

        private MpmcBoundedBuffer<T>[] buffers;

        public StripedMpmcBuffer(int stripeCount, int bufferSize)
            : this(new StripedBufferSize(bufferSize, stripeCount))
        {
        }

        public StripedMpmcBuffer(StripedBufferSize bufferSize)
        {
            buffers = new MpmcBoundedBuffer<T>[bufferSize.StripeCount];

            for (var i = 0; i < bufferSize.StripeCount; i++)
            {
                buffers[i] = new MpmcBoundedBuffer<T>(bufferSize.BufferSize);
            }
        }

        public int Capacity => buffers.Length * buffers[0].Capacity;

        public int DrainTo(T[] outputBuffer)
        {
            var count = 0;

            for (var i = 0; i < buffers.Length; i++)
            {
                var status = BufferStatus.Full;

                while (count < outputBuffer.Length & status != BufferStatus.Empty)
                {
                    status = buffers[i].TryTake(out var item);

                    if (status == BufferStatus.Success)
                    {
                        outputBuffer[count++] = item;
                    }
                }
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
