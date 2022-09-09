using System.Runtime.InteropServices;
using System.Threading;

namespace BitFaster.Caching.Counters
{
    /// <summary>
    /// A long value padded by the size of a CPU cache line to mitigate false sharing.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 2 * Padding.CACHE_LINE_SIZE)] // padding before/between/after fields
    public struct PaddedLong
    {
        /// <summary>
        /// The value.
        /// </summary>
        [FieldOffset(Padding.CACHE_LINE_SIZE)] public long value;

        /// <summary>
        /// Reads the value of the field, and on systems that require it inserts a memory barrier to 
        /// prevent reordering of memory operations.
        /// </summary>
        /// <returns>The value that was read.</returns>
        public long VolatileRead()
        {
            return Volatile.Read(ref this.value);
        }

        /// <summary>
        /// Compares the current value with an expected value, if they are equal replaces the current value.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="updated">The updated value.</param>
        /// <returns>True if the value is updated, otherwise false.</returns>
        public bool CompareAndSwap(long expected, long updated)
        {
            return Interlocked.CompareExchange(ref this.value, updated, expected) == expected;
        }
    }
}
