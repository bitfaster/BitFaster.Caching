using System;

namespace BitFaster.Caching.Lru
{
    internal static class Defaults
    {
        public static int ConcurrencyLevel => Environment.ProcessorCount;
    }
}
