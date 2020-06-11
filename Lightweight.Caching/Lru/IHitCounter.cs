using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
    public interface IHitCounter
    {
        void IncrementTotalCount();

        void IncrementHitCount();

        double HitRatio { get; }
    }
}
