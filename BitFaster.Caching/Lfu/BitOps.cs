using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace BitFaster.Caching.Lfu
{
    public class BitOps
    {
        public static int CeilingPowerOfTwo(int x)
        {
            return CeilingPowerOfTwo((uint)x);
        }

        public static int CeilingPowerOfTwo(uint x)
        {
#if NETSTANDARD2_0
            //int result = 2;
            //while (result < x)
            //{
            //    result <<= 1;
            //}

            //return result;
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return (int)x + 1;
#else
            return 1 << -BitOperations.LeadingZeroCount(x - 1);
#endif

        }

        public static int BitCount(int x)
        {
            return BitCount((uint)x);
        }

        public static int BitCount(uint x)
        {
#if NETSTANDARD2_0
            int count = 0;
            while (x != 0)
            {
                count++;
                x &= (x - 1); //walking through all the bits which are set to one
            }

            return count;
#else
            return BitOperations.PopCount(x);
#endif
        }

        public static int BitCount(long x)
        {
            return BitCount((ulong)x);
        }

        public static int BitCount(ulong x)
        {
#if NETSTANDARD2_0
            int count = 0;
            while (x != 0)
            {
                count++;
                x &= (x - 1); //walking through all the bits which are set to one
            }

            return count;
#else
            return BitOperations.PopCount(x);
#endif
        }
    }
}
