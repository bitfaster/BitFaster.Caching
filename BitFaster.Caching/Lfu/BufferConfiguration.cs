
using System;

namespace BitFaster.Caching.Lfu
{
    public class BufferConfiguration
    {
        private const int MaxReadBufferTotalSize = 4096;
        private const int MaxWriteBufferTotalSize = 2048;

        public int ReadBufferStripes { get; set; }

        public int ReadBufferSize { get; set; }

        public int WriteBufferStripes { get; set; }

        public int WriteBufferSize { get; set; }

        public static BufferConfiguration CreateDefault(int concurrencyLevel, int capacity)
        {
            concurrencyLevel = BitOps.CeilingPowerOfTwo(concurrencyLevel);

            // Estimate total read buffer size based on capacity and concurrency, up to a maximum of MaxReadBufferTotalSize.
            // Stripe based on concurrency, with a minimum and maximum buffer size (between 4 and 128).
            // Total size becomes 4 * concurrency level when concurrencyLevel * capacity > MaxReadBufferTotalSize.
            int readBufferTotalSize = Math.Min(BitOps.CeilingPowerOfTwo(concurrencyLevel * capacity), MaxReadBufferTotalSize);
            int readStripeSize = Math.Min(BitOps.CeilingPowerOfTwo(Math.Max(readBufferTotalSize / concurrencyLevel, 4)), 128);

            // Try to constrain write buffer size so that the LFU dictionary will not ever end up with more than 2x cache
            // capacity entries before maintenance runs.
            int writeBufferTotalSize = Math.Min(BitOps.CeilingPowerOfTwo(capacity), MaxWriteBufferTotalSize);
            int writeStripeSize = Math.Min(BitOps.CeilingPowerOfTwo(Math.Max(writeBufferTotalSize / concurrencyLevel, 4)), 128);

            return new BufferConfiguration()
            {
                ReadBufferStripes = concurrencyLevel,
                ReadBufferSize = 128,
                WriteBufferStripes = concurrencyLevel,
                WriteBufferSize = writeStripeSize,
            };
        }
    }
}
