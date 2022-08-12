using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace BitFaster.Caching.Lfu
{
    // TODO: .NET Core only
    public class Tools
    {
        public static int CeilingPowerOfTwo(int x)
        {
            return CeilingPowerOfTwo((uint)x);
        }

        public static int CeilingPowerOfTwo(uint x)
        {
#if NETSTANDARD2_0
            return 0;
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
            return 0;
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
            return 0;
#else
            return BitOperations.PopCount(x);
#endif
        }
    }
}
