using System;

namespace BitFaster.Caching.Buffers
{
    public sealed class StripedBufferSize
    {
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

        public int BufferSize { get; }

        public int StripeCount { get; }
    }
}
