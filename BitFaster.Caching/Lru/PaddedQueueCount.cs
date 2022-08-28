using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BitFaster.Caching.Lru
{
    [DebuggerDisplay("Hot = {hot}, Warm = {warm}, Cold = {cold}")]
    [StructLayout(LayoutKind.Explicit, Size = 4 * Padding.CACHE_LINE_SIZE)] // padding before/between/after fields
    internal struct PaddedQueueCount
    {
        [FieldOffset(1 * Padding.CACHE_LINE_SIZE)] public int hot;
        [FieldOffset(2 * Padding.CACHE_LINE_SIZE)] public int warm;
        [FieldOffset(3 * Padding.CACHE_LINE_SIZE)] public int cold;
    }
}
