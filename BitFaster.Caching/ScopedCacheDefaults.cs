using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    internal static class ScopedCacheDefaults
    {
        internal const int MaxRetry = 5;
        internal static readonly string RetryFailureMessage = $"Exceeded {MaxRetry} attempts to create a lifetime.";
    }
}
