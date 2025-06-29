using System;

namespace BitFaster.Caching.Lru
{
    internal static class Defaults
    {
#if NET8_0_OR_GREATER
        public static int ConcurrencyLevel => -1;
#else
        public static int ConcurrencyLevel => Environment.ProcessorCount;
#endif
    }
}
