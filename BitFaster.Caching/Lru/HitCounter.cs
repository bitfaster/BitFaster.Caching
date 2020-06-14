using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public struct HitCounter : IHitCounter
    {
        private long requestHitCount;
        private long requestTotalCount;

        public double HitRatio => requestTotalCount == 0 ? 0 : (double)requestHitCount / (double)requestTotalCount;

        public void IncrementTotalCount()
        {
            Interlocked.Increment(ref this.requestTotalCount);
        }

        public void IncrementHitCount()
        {
            Interlocked.Increment(ref this.requestHitCount);
        }
    }
}
