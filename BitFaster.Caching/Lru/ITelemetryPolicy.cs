using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public interface ITelemetryPolicy<K, V> : ICacheMetrics
    {
        void IncrementMiss();

        void IncrementHit();

        void OnItemRemoved(K key, V value, ItemRemovedReason reason);

        void SetEventSource(object source);
    }
}
