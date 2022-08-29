
using System.Runtime.InteropServices;
using System.Threading;

namespace BitFaster.Caching.Pad
{
    [StructLayout(LayoutKind.Explicit, Size = 2 * Padding.CACHE_LINE_SIZE)] // padding before/between/after fields
    public struct PaddedLong
    {
        [FieldOffset(Padding.CACHE_LINE_SIZE)] public long value;

        public long GetValue()
        {
            return Volatile.Read(ref this.value);
        }

        public long NonVolatileGetValue()
        {
            return this.value;
        }

        public void SetValue(long value)
        {
            Volatile.Write(ref this.value, value);
        }

        public void NonVolatileSetValue(long value)
        {
            this.value = value;
        }

        public long Increment()
        {
            return Interlocked.Increment(ref this.value);
        }

        public bool CompareAndSwap(long expected, long updated)
        {
            return Interlocked.CompareExchange(ref this.value, updated, expected) == expected;
        }
    }
}
