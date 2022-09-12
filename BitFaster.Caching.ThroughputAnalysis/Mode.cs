using System;

namespace BitFaster.Caching.ThroughputAnalysis
{
    [Flags]
    public enum Mode
    {
        Read = 1,
        ReadWrite = 2,
        Evict = 4,
        Update = 8,
        All = ~0,
    }
}
