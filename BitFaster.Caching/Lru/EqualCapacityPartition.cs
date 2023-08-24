using System;
using System.Diagnostics;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// A simple partitioning scheme to put an approximately equal number of items in each queue.
    /// </summary>
    [DebuggerDisplay("{Hot}/{Warm}/{Cold}")]
    public class EqualCapacityPartition : ICapacityPartition
    {
        private readonly int hotCapacity;
        private readonly int warmCapacity;
        private readonly int coldCapacity;

        /// <summary>
        /// Initializes a new instance of the EqualCapacityPartition class with the specified total capacity.
        /// </summary>
        /// <param name="totalCapacity">The total capacity.</param>
        public EqualCapacityPartition(int totalCapacity)
        {
            var (hot, warm, cold) = ComputeQueueCapacity(totalCapacity);
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

        private static (int hot, int warm, int cold) ComputeQueueCapacity(int capacity)
        {
            if (capacity < 3)
                Throw.ArgOutOfRange(nameof(capacity), "Capacity must be greater than or equal to 3.");

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
