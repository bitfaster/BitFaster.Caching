using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public enum Mode
    {
        Read,
        ReadWrite,
        Evict,
        Update,
    }
}
