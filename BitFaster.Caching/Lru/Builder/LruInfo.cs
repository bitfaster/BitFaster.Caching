using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru.Builder
{
    public sealed class LruInfo<K>
    {
        public ICapacityPartition Capacity { get; set; } = new FavorWarmPartition(128);

        public int ConcurrencyLevel { get; set; } = Defaults.ConcurrencyLevel;

        public TimeSpan? TimeToExpireAfterWrite { get; set; } = null;

        public bool WithMetrics { get; set; } = false;

        public IEqualityComparer<K> KeyComparer { get; set; } = EqualityComparer<K>.Default;
    }
}
