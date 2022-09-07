using System;

namespace BitFaster.Caching.Lru
{
    /// <summary>
    /// Extension methods for ICapacityPartition.
    /// </summary>
    public static class CapacityPartitionExtensions
    {
        /// <summary>
        /// Validates the specified capacity partition.
        /// </summary>
        /// <param name="capacity">The capacity partition to validate.</param>
        /// <exception cref="ArgumentOutOfRangeException">Any of the hot, warm or cold capacities is less than 1.</exception>
        public static void Validate(this ICapacityPartition capacity)
        {
            if (capacity.Cold < 1)
            { 
                throw new ArgumentOutOfRangeException(nameof(capacity.Cold));
            }

            if (capacity.Warm < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity.Warm));
            }

            if (capacity.Hot < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity.Hot));
            }
        }
    }
}
