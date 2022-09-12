using System;
using System.Diagnostics;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// A capacity partitioning scheme that favors frequently accessed items by allocating 80% 
    /// capacity to the warm queue.
    /// </summary>
    [DebuggerDisplay("{Hot}/{Warm}/{Cold}")]
    public class FavorWarmPartition : ICapacityPartition
    {
        private readonly int hotCapacity;
        private readonly int warmCapacity;
        private readonly int coldCapacity;

        /// <summary>
        /// Default to 80% capacity allocated to warm queue, 20% split equally for hot and cold.
        /// This favors frequently accessed items.
        /// </summary>
        public const double DefaultWarmRatio = 0.8;

        /// <summary>
        /// Initializes a new instance of the FavorWarmPartition class with the specified capacity and the default warm ratio.
        /// </summary>
        /// <param name="totalCapacity">The total capacity.</param>
        public FavorWarmPartition(int totalCapacity)
            : this(totalCapacity, DefaultWarmRatio)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FavorWarmPartition class with the specified capacity and warm ratio.
        /// </summary>
        /// <param name="totalCapacity">The total capacity.</param>
        /// <param name="warmRatio">The ratio of warm items to hot and cold items.</param>
        public FavorWarmPartition(int totalCapacity, double warmRatio)
        {
            var (hot, warm, cold) = ComputeQueueCapacity(totalCapacity, warmRatio);
            this.hotCapacity = hot;
            this.warmCapacity = warm;
            this.coldCapacity = cold;
        }

        ///<inheritdoc/>
        public int Cold => this.coldCapacity;

        ///<inheritdoc/>
        public int Warm => this.warmCapacity;

        ///<inheritdoc/>
        public int Hot => this.hotCapacity;

        private static (int hot, int warm, int cold) ComputeQueueCapacity(int capacity, double warmRatio)
        {
            if (capacity < 3)
            {
                Ex.ThrowArgOutOfRange(nameof(capacity), "Capacity must be greater than or equal to 3.");
            }

            if (warmRatio <= 0.0 || warmRatio >= 1.0)
            {
                Ex.ThrowArgOutOfRange(nameof(warmRatio), "warmRatio must be between 0.0 and 1.0");
            }

            int warm2 = (int)(capacity * warmRatio);
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
