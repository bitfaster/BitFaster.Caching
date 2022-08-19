using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching
{
    public enum BufferStatus
    {
        Full,
        Empty,
        Success,
        Contended,
    }
}
