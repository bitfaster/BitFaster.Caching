
using System;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// During reads, the policy evaluates ShouldDiscard and Touch. To avoid Getting the current time twice
    /// introduce a simple clock that holds the last time. This is class with a mutable field, because the 
    /// policy structs are readonly.
    /// </summary>
    internal class Time
    {
        internal static readonly TimeSpan MaxRepresentable = TimeSpan.FromTicks(9223372036854769664);

        /// <summary>
        /// Gets or sets the last time.
        /// </summary>
        internal long Last { get; set; }
    }
}
