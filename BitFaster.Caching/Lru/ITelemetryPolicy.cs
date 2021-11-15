using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public interface ITelemetryPolicy<K, V>
    {
        void IncrementMiss();

        void IncrementHit();

        void OnItemRemoved(K key, V value, ItemRemovedReason reason);

        double HitRatio { get; }

        void SetEventSource(object source);
    }
}
