using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public interface IHitCounter<K, V>
    {
        void IncrementMiss();

        void IncrementHit();

        void OnItemRemoved(K key, V value);

        double HitRatio { get; }
    }
}
