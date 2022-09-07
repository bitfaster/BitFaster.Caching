using System;

namespace BitFaster.Caching.Buffers
{
    /// <summary>
    /// Represents the size of a striped buffer.
    /// </summary>
    public sealed class StripedBufferSize
    {
        /// <summary>
        /// Initializes a new instance of the StripedBufferSize class with the specified buffer size and stripe count.
        /// </summary>
        /// <param name="bufferSize">The size of each striped buffer.</param>
        /// <param name="stripeCount">The number of stripes.</param>
        public StripedBufferSize(int bufferSize, int stripeCount)
        {
            if (bufferSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            if (stripeCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(stripeCount));
            }

            BufferSize = BitOps.CeilingPowerOfTwo(bufferSize);
            StripeCount = BitOps.CeilingPowerOfTwo(stripeCount);
        }

        /// <summary>
        /// The size of the buffer. Each stripe will be initialized with a buffer of this size.
        /// </summary>
        public int BufferSize { get; }

        /// <summary>
        /// The number of stripes.
        /// </summary>
        public int StripeCount { get; }
    }
}
