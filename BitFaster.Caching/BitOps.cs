using System.Numerics;

namespace BitFaster.Caching
{
    /// <summary>
    /// Provides utility methods for bit-twiddling operations.
    /// </summary>
    public static class BitOps
    {
        /// <summary>
        /// Calculate the smallest power of 2 greater than the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>Smallest power of two greater than or equal to x.</returns>
        public static int CeilingPowerOfTwo(int x)
        {
            return (int)CeilingPowerOfTwo((uint)x);
        }

        /// <summary>
        /// Calculate the smallest power of 2 greater than the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>Smallest power of two greater than or equal to x.</returns>
        internal static long CeilingPowerOfTwo(long x)
        {
            return (long)CeilingPowerOfTwo((ulong)x);
        }

        /// <summary>
        /// Calculate the smallest power of 2 greater than the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>Smallest power of two greater than or equal to x.</returns>
        public static uint CeilingPowerOfTwo(uint x)
        {
#if NETSTANDARD2_0
            // https://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
#else
            return BitOperations.RoundUpToPowerOf2(x);
#endif
        }

        /// <summary>
        /// Calculate the smallest power of 2 greater than the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>Smallest power of two greater than or equal to x.</returns>
        internal static ulong CeilingPowerOfTwo(ulong x)
        {
#if NETSTANDARD2_0
            // https://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            x |= x >> 32;
            return x + 1;
#else
            return BitOperations.RoundUpToPowerOf2(x);
#endif
        }

        /// <summary>
        /// Counts the number of trailing zero bits in the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>The number of trailing zero bits.</returns>
        internal static int TrailingZeroCount(long x)
        {
            return TrailingZeroCount((ulong)x);
        }

        /// <summary>
        /// Counts the number of trailing zero bits in the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>The number of trailing zero bits.</returns>
        internal static int TrailingZeroCount(ulong x)
        {
#if NETSTANDARD2_0
            // https://codereview.stackexchange.com/questions/288007/c-bit-utility-functions-popcount-trailing-zeros-count-reverse-all-bits
            return BitCount(~x & (x - 1));
#else
            return BitOperations.TrailingZeroCount(x);
#endif
        }

        /// <summary>
        /// Counts the number of 1 bits in the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>The number of 1 bits.</returns>
        public static int BitCount(int x)
        {
            return BitCount((uint)x);
        }

        /// <summary>
        /// Counts the number of 1 bits in the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>The number of 1 bits.</returns>
        public static int BitCount(uint x)
        {
#if NETSTANDARD2_0
            const uint c1 = 0x_55555555u;
            const uint c2 = 0x_33333333u;
            const uint c3 = 0x_0F0F0F0Fu;
            const uint c4 = 0x_01010101u;

            x -= (x >> 1) & c1;
            x = (x & c2) + ((x >> 2) & c2);
            x = (((x + (x >> 4)) & c3) * c4) >> 24;

            return (int)x;
#else
            return BitOperations.PopCount(x);
#endif
        }

        /// <summary>
        /// Counts the number of 1 bits in the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>The number of 1 bits.</returns>
        public static int BitCount(long x)
        {
            return BitCount((ulong)x);
        }

        /// <summary>
        /// Counts the number of 1 bits in the input parameter.
        /// </summary>
        /// <param name="x">The input parameter.</param>
        /// <returns>The number of 1 bits.</returns>
        public static int BitCount(ulong x)
        {
#if NETSTANDARD2_0
            const ulong c1 = 0x_55555555_55555555ul;
            const ulong c2 = 0x_33333333_33333333ul;
            const ulong c3 = 0x_0F0F0F0F_0F0F0F0Ful;
            const ulong c4 = 0x_01010101_01010101ul;

            x -= (x >> 1) & c1;
            x = (x & c2) + ((x >> 2) & c2);
            x = (((x + (x >> 4)) & c3) * c4) >> 56;

            return (int)x;
#else
            return BitOperations.PopCount(x);
#endif
        }

        /// <summary>
        /// Computes Stafford variant 13 of 64-bit mix function.
        /// </summary>
        /// <param name="z">The input parameter.</param>
        /// <returns>A bit mix of the input parameter.</returns>
        /// <remarks>
        /// See http://zimbry.blogspot.com/2011/09/better-bit-mixing-improving-on.html
        /// </remarks>
        public static ulong Mix64(ulong z)
        {
            z = (z ^ z >> 30) * 0xbf58476d1ce4e5b9L;
            z = (z ^ z >> 27) * 0x94d049bb133111ebL;
            return z ^ z >> 31;
        }
    }
}
