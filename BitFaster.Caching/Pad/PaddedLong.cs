
using System.Runtime.InteropServices;
using System.Threading;

namespace BitFaster.Caching.Pad
{
    [StructLayout(LayoutKind.Explicit, Size = 2 * Padding.CACHE_LINE_SIZE)] // padding before/between/after fields
    public struct PaddedLong
    {
        [FieldOffset(Padding.CACHE_LINE_SIZE)] public long value;

        public long VolatileRead()
        {
            return Volatile.Read(ref this.value);
        }

        public bool CompareAndSwap(long expected, long updated)
        {
            return Interlocked.CompareExchange(ref this.value, updated, expected) == expected;
        }
    }
}
