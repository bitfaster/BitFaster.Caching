using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace BitFaster.Caching.Lfu
{
    public class LfuCapacityPartition
    {
        private readonly int windowCapacity;
        private readonly int protectedCapacity;
        private readonly int probationCapacity;

        public LfuCapacityPartition(int totalCapacity)
        {
            (windowCapacity, protectedCapacity, probationCapacity) = ComputeQueueCapacity(totalCapacity);
        }

        public int Window => this.windowCapacity;

        public int Protected => this.protectedCapacity;

        public int Probation => this.probationCapacity;

        public int Capacity => this.windowCapacity + this.protectedCapacity + this.probationCapacity;

        private static (int window, int mainProtected, int mainProbation) ComputeQueueCapacity(int capacity)
        {
            if (capacity < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than or equal to 3.");
            }

            int window = capacity - (int)(0.99 * capacity);
            int mainProtected = (int)(0.8 * (capacity - window));
            int mainProbation = capacity - window - mainProtected;

            return (window, mainProtected, mainProbation);
        }
    }
}
