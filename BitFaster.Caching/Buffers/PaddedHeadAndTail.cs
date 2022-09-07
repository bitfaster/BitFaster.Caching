using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BitFaster.Caching.Buffers
{
    [DebuggerDisplay("Head = {Head}, Tail = {Tail}")]
    [StructLayout(LayoutKind.Explicit, Size = 3 * Padding.CACHE_LINE_SIZE)] // padding before/between/after fields
    internal struct PaddedHeadAndTail
    {
        [FieldOffset(1 * Padding.CACHE_LINE_SIZE)] public int Head;
        [FieldOffset(2 * Padding.CACHE_LINE_SIZE)] public int Tail;
    }
}
