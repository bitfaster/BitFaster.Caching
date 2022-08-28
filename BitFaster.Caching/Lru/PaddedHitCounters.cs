using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace BitFaster.Caching.Lru
{
    [StructLayout(LayoutKind.Explicit, Size = 5 * Padding.CACHE_LINE_SIZE)] // padding before/between/after fields
    public struct PaddedHitCounters
    {
        [FieldOffset(1 * Padding.CACHE_LINE_SIZE)] public long hitCount;
        [FieldOffset(2 * Padding.CACHE_LINE_SIZE)] public long missCount;
        [FieldOffset(3 * Padding.CACHE_LINE_SIZE)] public long evictedCount;
        [FieldOffset(4 * Padding.CACHE_LINE_SIZE)] public long updatedCount;
    }
}
