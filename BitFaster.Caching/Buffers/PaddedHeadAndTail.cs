using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace BitFaster.Caching.Buffers
{
    [DebuggerDisplay("Head = {Head}, Tail = {Tail}")]
    [StructLayout(LayoutKind.Explicit, Size = 3 * Padding.CACHE_LINE_SIZE)] // padding before/between/after fields
    internal struct PaddedHeadAndTail
    {
        [FieldOffset(1 * Padding.CACHE_LINE_SIZE)] public volatile int Head;
        [FieldOffset(2 * Padding.CACHE_LINE_SIZE)] public volatile int Tail;
    }
}
