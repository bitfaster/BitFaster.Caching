using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public interface IHitCounter
    {
        void IncrementMiss();

        void IncrementHit();

        double HitRatio { get; }
    }
}
