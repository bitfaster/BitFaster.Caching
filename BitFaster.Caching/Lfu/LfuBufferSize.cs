
using System;
using BitFaster.Caching.Buffers;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// Represents the read and write buffer sizes used to initialize ConcurrentLfu.
    /// </summary>
    public class LfuBufferSize
    {
        /// <summary>
        /// The default buffer size.
        /// </summary>
        public const int DefaultBufferSize = 128;

        /// <summary>
        /// Initializes a new instance of the LfuBufferSize class with the specified read and write buffer sizes.
        /// </summary>
        /// <param name="readBufferSize">The read buffer size.</param>
        public LfuBufferSize(StripedBufferSize readBufferSize)
        {
            Read = readBufferSize ?? throw new ArgumentNullException(nameof(readBufferSize));
        }

        /// <summary>
        /// Gets the read buffer size.
        /// </summary>
        public StripedBufferSize Read { get; }

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
                    new StripedBufferSize(32, 1));
            }

            // cap concurrency at proc count * 2
            concurrencyLevel = Math.Min(BitOps.CeilingPowerOfTwo(concurrencyLevel), BitOps.CeilingPowerOfTwo(Environment.ProcessorCount * 2));

            // cap read buffer at aprrox 10x total capacity
            while (concurrencyLevel * DefaultBufferSize > BitOps.CeilingPowerOfTwo(capacity * 10))
            {
                concurrencyLevel /= 2;
            }

            return new LfuBufferSize(
                new StripedBufferSize(DefaultBufferSize, concurrencyLevel));
        }
    }
}
