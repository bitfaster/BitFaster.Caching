using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// A simple partitioning scheme to put an approximately equal number of items in each queue.
    /// </summary>
    public class EqualCapacityPartition : ICapacityPartition
    {
        private readonly int hotCapacity;
        private readonly int warmCapacity;
        private readonly int coldCapacity;

        public EqualCapacityPartition(int totalCapacity)
        {
            var (hot, warm, cold) = ComputeQueueCapacity(totalCapacity);
            this.hotCapacity = hot;
            this.warmCapacity = warm;
            this.coldCapacity = cold;
        }

        public int Cold => this.coldCapacity;

        public int Warm => this.warmCapacity;

        public int Hot => this.hotCapacity;

        private static (int hot, int warm, int cold) ComputeQueueCapacity(int capacity)
        {
            if (capacity < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than or equal to 3.");
            }

            int hotCapacity = capacity / 3;
            int warmCapacity = capacity / 3;
            int coldCapacity = capacity / 3;

            int remainder = capacity % 3;

            // favor warm, then cold
            switch (remainder)
            {
                case 1:
                    warmCapacity++;
                    break;
                case 2:
                    warmCapacity++;
                    coldCapacity++;
                    break;
            }

            return (hotCapacity, warmCapacity, coldCapacity);
        }
    }
}
