using System;
using System.Diagnostics;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// Represents the LFU capacity partition. Uses a hill climbing algorithm to optimze partition sizes over time.
    /// </summary>
    [DebuggerDisplay("{Capacity} ({Window}/{Protected}/{Probation})")]
    public sealed class LfuCapacityPartition
    {
        private readonly int max;
        
        private int windowCapacity;
        private int protectedCapacity;
        private int probationCapacity;

        private double previousHitRate;
        private long previousHitCount;
        private long previousMissCount;

        private double mainRatio = DefaultMainPercentage;
        private double stepSize;

        private const double HillClimberRestartThreshold = 0.05d;
        private const double HillClimberStepPercent = 0.0625d;
        private const double HillClimberStepDecayRate = 0.98d;

        private const double DefaultMainPercentage = 0.99d;

        private const double MaxMainPercentage = 0.999d;
        private const double MinMainPercentage = 0.2d;

        /// <summary>
        /// Initializes a new instance of the LfuCapacityPartition class with the specified total capacity.
        /// </summary>
        /// <param name="totalCapacity">The total capacity.</param>
        public LfuCapacityPartition(int totalCapacity)
        {
            this.max = totalCapacity;
            (windowCapacity, protectedCapacity, probationCapacity) = ComputeQueueCapacity(totalCapacity, DefaultMainPercentage);
            InitializeStepSize();

            previousHitRate = 1.0;
        }

        /// <summary>
        /// Gets the number of items permitted in the window LRU.
        /// </summary>
        public int Window => this.windowCapacity;

        /// <summary>
        /// Gets the number of items permitted in the protected LRU.
        /// </summary>
        public int Protected => this.protectedCapacity;

        /// <summary>
        /// Gets the number of items permitted in the probation LRU.
        /// </summary>
        public int Probation => this.probationCapacity;

        /// <summary>
        /// Gets the total capacity.
        /// </summary>
        public int Capacity => this.max;


        /// <summary>
        /// Optimize the size of the window and main LRUs based on changes in hit rate.
        /// </summary>
        /// <param name="metrics">The cache metrics.</param>
        /// <param name="sampleThreshold">The number of cache requests to sample before attempting to optimize LRU sizes.</param>
        /// <remarks>
        /// window = recency-biased, main = frequency-biased.
        /// </remarks>
        public void OptimizePartitioning(ICacheMetrics metrics, int sampleThreshold)
        {
            long newHits = metrics.Hits;
            long newMisses = metrics.Misses;

            long sampleHits = newHits - previousHitCount;
            long sampleMisses = newMisses - previousMissCount;
            long sampleCount = sampleHits + sampleMisses;

            if (sampleCount < sampleThreshold)
            {
                return;
            }

            double sampleHitRate = (double)sampleHits / sampleCount;

            double hitRateChange = sampleHitRate - previousHitRate;
            double amount = (hitRateChange >= 0) ? stepSize : -stepSize;

            double nextStepSize = (Math.Abs(hitRateChange) >= HillClimberRestartThreshold)
                  ? HillClimberStepPercent * (amount >= 0 ? 1 : -1)
                  : HillClimberStepDecayRate * amount;

            stepSize = nextStepSize;

            previousHitCount = newHits;
            previousMissCount = newMisses;
            previousHitRate = sampleHitRate;

            mainRatio -= amount;
            mainRatio = Clamp(mainRatio, MinMainPercentage, MaxMainPercentage);

            (windowCapacity, protectedCapacity, probationCapacity) = ComputeQueueCapacity(max, mainRatio);
        }

        private void InitializeStepSize()
        {
            stepSize = HillClimberStepPercent;
        }

        private double Clamp(double input, double min, double max)
        {
            return Math.Max(min, Math.Min(input, max));
        }

        private static (int window, int mainProtected, int mainProbation) ComputeQueueCapacity(int capacity, double mainPercentage)
        {
            if (capacity < 3)
            {
                Throw.ArgOutOfRange(nameof(capacity), "Capacity must be greater than or equal to 3.");
            }

            int window = capacity - (int)(mainPercentage * capacity);
            int mainProtected = (int)(0.8 * (capacity - window));
            int mainProbation = capacity - window - mainProtected;

            return (window, mainProtected, mainProbation);
        }
    }
}
