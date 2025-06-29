using System;

namespace BitFaster.Caching.Lru
{
    internal static class Defaults
    {
#if NET8_0_OR_GREATER
        // Note that on .net8+, -1 indicates the default concurrency level
        public static int ConcurrencyLevel => -1;
#else
        public static int ConcurrencyLevel => Environment.ProcessorCount;
#endif
    }
}
