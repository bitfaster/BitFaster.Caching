
using System;
using BitFaster.Caching.Buffers;

namespace BitFaster.Caching.Lfu
{
    public class LfuBufferSize
    {
        public const int DefaultBufferSize = 128;

        private const int MaxWriteBufferTotalSize = 1024;

        public LfuBufferSize(StripedBufferSize readBufferSize, StripedBufferSize writeBufferSize)
        {
            Read = readBufferSize ?? throw new ArgumentNullException(nameof(readBufferSize));
            Write = writeBufferSize ?? throw new ArgumentNullException(nameof(writeBufferSize));
        }

        /// <summary>
        /// Gets the read buffer size.
        /// </summary>
        public StripedBufferSize Read { get; }

        /// <summary>
        /// Gets the write buffer size.
        /// </summary>
        public StripedBufferSize Write { get; }

        /// <summary>
        /// Estimates default buffer sizes intended to give optimal throughput.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will use the cache concurrently.</param>
        /// <param name="capacity">The capacity of the cache. The size of the write buffer is constained to avoid the cache growing to greater than 2x capacity while writes are buffered.</param>
        /// <returns>An LruBufferSize</returns>
        public static LfuBufferSize Default(int concurrencyLevel, int capacity)
        {
            if (capacity < 13)
            {
                return new LfuBufferSize(
                    new StripedBufferSize(32, 1),
                    new StripedBufferSize(16, 1));
            }

            // cap concurrency at proc count * 2
            concurrencyLevel = Math.Min(BitOps.CeilingPowerOfTwo(concurrencyLevel), BitOps.CeilingPowerOfTwo(Environment.ProcessorCount * 2));

            // cap read buffer at aprrox 10x total capacity
            while (concurrencyLevel * DefaultBufferSize > BitOps.CeilingPowerOfTwo(capacity * 10))
            {
                concurrencyLevel /= 2;
            }

            // Constrain write buffer size so that the LFU dictionary will not ever end up with more than 2x cache
            // capacity entries before maintenance runs.
            int writeBufferTotalSize = Math.Min(BitOps.CeilingPowerOfTwo(capacity), MaxWriteBufferTotalSize);
            int writeStripeSize = Math.Min(BitOps.CeilingPowerOfTwo(Math.Max(writeBufferTotalSize / concurrencyLevel, 4)), 128);

            return new LfuBufferSize(
                new StripedBufferSize(DefaultBufferSize, concurrencyLevel),
                new StripedBufferSize(writeStripeSize, concurrencyLevel));
        }
    }
}
