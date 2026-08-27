using System;
using System.Diagnostics;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// Represents the count-based LFU capacity partition and provides the common hill climbing state
    /// used by LFU capacity partitions.
    /// </summary>
    [DebuggerDisplay("{Capacity} ({Window}/{Protected}/{Probation})")]
    public class LfuCapacityPartition
    {
        private readonly int max;

        private protected long windowMaximum;
        private protected long mainProtectedMaximum;

        private double previousHitRate;
        private long previousHitCount;
        private long previousMissCount;

        private double mainRatio = DefaultMainPercentage;
        private double stepSize;

        private const double HillClimberRestartThreshold = 0.05d;
        private protected const double HillClimberStepPercent = 0.0625d;
        private const double HillClimberStepDecayRate = 0.98d;

        private const double DefaultMainPercentage = 0.99d;
        private const double MainProtectedPercentage = 0.8d;

        private const double MaxMainPercentage = 0.999d;
        private const double MinMainPercentage = 0.2d;

        /// <summary>
        /// Initializes a new instance of the LfuCapacityPartition class with the specified total capacity.
        /// </summary>
        /// <param name="totalCapacity">The total capacity.</param>
        public LfuCapacityPartition(int totalCapacity)
            : this(ValidateCapacity(totalCapacity), HillClimberStepPercent)
        {
        }

        private protected LfuCapacityPartition(int totalCapacity, double initialStepSize)
        {
            this.max = totalCapacity;
            this.stepSize = initialStepSize;
            this.previousHitRate = 1.0d;
            SetMaximums(DefaultMainPercentage);
        }

        /// <summary>
        /// Gets the number of items permitted in the window LRU.
        /// </summary>
        public int Window => (int)this.windowMaximum;

        /// <summary>
        /// Gets the number of items permitted in the protected LRU.
        /// </summary>
        public int Protected => (int)this.mainProtectedMaximum;

        /// <summary>
        /// Gets the number of items permitted in the probation LRU.
        /// </summary>
        public int Probation => this.max - this.Window - this.Protected;

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
            if (!TryGetAdjustment(metrics, sampleThreshold, HillClimberStepPercent, out double adjustment))
            {
                return;
            }

            this.mainRatio = Clamp(this.mainRatio - adjustment, MinMainPercentage, MaxMainPercentage);
            SetMaximums(this.mainRatio);
        }

        private protected bool TryGetAdjustment(ICacheMetrics metrics, int sampleThreshold, double restartStepSize, out double adjustment)
        {
            long newHits = metrics.Hits;
            long newMisses = metrics.Misses;

            long sampleHits = newHits - this.previousHitCount;
            long sampleMisses = newMisses - this.previousMissCount;
            long sampleCount = sampleHits + sampleMisses;

            if (sampleCount < sampleThreshold)
            {
                adjustment = 0;
                return false;
            }

            double sampleHitRate = (double)sampleHits / sampleCount;

            double hitRateChange = sampleHitRate - this.previousHitRate;
            adjustment = (hitRateChange >= 0) ? this.stepSize : -this.stepSize;

            double nextStepSize = (Math.Abs(hitRateChange) >= HillClimberRestartThreshold)
                ? CopySign(restartStepSize, adjustment)
                : HillClimberStepDecayRate * adjustment;

            this.stepSize = nextStepSize;

            this.previousHitCount = newHits;
            this.previousMissCount = newMisses;
            this.previousHitRate = sampleHitRate;

            return true;
        }

        private void SetMaximums(double mainPercentage)
        {
            this.windowMaximum = this.max - (long)(mainPercentage * this.max);
            this.mainProtectedMaximum = (long)(MainProtectedPercentage * (this.max - this.windowMaximum));
        }

        private static double Clamp(double input, double min, double max)
        {
            return Math.Max(min, Math.Min(input, max));
        }

        private static double CopySign(double magnitude, double sign)
        {
            return (sign < 0) ? -Math.Abs(magnitude) : Math.Abs(magnitude);
        }

        private static int ValidateCapacity(int capacity)
        {
            if (capacity < 3)
                Throw.ArgOutOfRange(nameof(capacity), "Capacity must be greater than or equal to 3.");

            return capacity;
        }
    }
}
