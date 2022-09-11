using System;

namespace BitFaster.Caching.ThroughputAnalysis
{
    [Flags]
    public enum Mode
    {
        Read = 0,
        ReadWrite = 1,
        Evict = 2,
        Update = 4,
        All = ~0,
    }
}
