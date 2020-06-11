using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
    public struct NoHitCounter : IHitCounter
    {
        public double HitRatio => 0.0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrementTotalCount()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrementHitCount()
        {
        }
    }
}
