using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public interface ICapacityPartition
    {
        int Cold { get; }
        
        int Warm { get; }

        int Hot { get; }

        int Total { get; }
    }

    public class EqualPartitioning : ICapacityPartition
    {
        private readonly int hotCapacity;
        private readonly int warmCapacity;
        private readonly int coldCapacity;

        public EqualPartitioning(int totalCapacity)
        {
            var (hot, warm, cold) = ComputeQueueCapacity(totalCapacity);
            this.hotCapacity = hot;
            this.warmCapacity = warm;
            this.coldCapacity = cold;
        }

        public int Cold => this.coldCapacity;

        public int Warm => this.warmCapacity;

        public int Hot => this.hotCapacity;

        public int Total => this.hotCapacity + this.warmCapacity + this.coldCapacity;

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

            switch (remainder)
            {
                case 1:
                    coldCapacity++;
                    break;
                case 2:
                    hotCapacity++;
                    coldCapacity++;
                    break;
            }

            return (hotCapacity, warmCapacity, coldCapacity);
        }
    }

    public class FavorWarmPartitioning : ICapacityPartition
    {
        private readonly int hotCapacity;
        private readonly int warmCapacity;
        private readonly int coldCapacity;

        // Default to 80% capacity allocated to warm queue, 20% split equally for hot and cold
        public FavorWarmPartitioning(int totalCapacity)
            : this(totalCapacity, 0.8)
        {
        }
        public FavorWarmPartitioning(int totalCapacity, double warmFactor)
        {
            var (hot, warm, cold) = ComputeQueueCapacity(totalCapacity, warmFactor);
            this.hotCapacity = hot;
            this.warmCapacity = warm;
            this.coldCapacity = cold;
        }

        public int Cold => this.coldCapacity;

        public int Warm => this.warmCapacity;

        public int Hot => this.hotCapacity;

        public int Total => this.hotCapacity + this.warmCapacity + this.coldCapacity;

        private static (int hot, int warm, int cold) ComputeQueueCapacity(int capacity, double warmFactor)
        {
            if (capacity < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than or equal to 3.");
            }

            int warm2 = (int)(capacity * warmFactor);
            int hot2 = (capacity - warm2) / 2;

            if (hot2 < 1)
            {
                hot2 = 1;
            }

            int cold2 = hot2;

            int overflow = warm2 + hot2 + cold2 - capacity;
            warm2 -= overflow;

            return (hot2, warm2, cold2);
        }
    }
}
