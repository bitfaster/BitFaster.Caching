using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.Lfu
{
    public class LfuCapacityPartition
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

        public LfuCapacityPartition(int totalCapacity)
        {
            this.max = totalCapacity;
            (windowCapacity, protectedCapacity, probationCapacity) = ComputeQueueCapacity(totalCapacity, DefaultMainPercentage);
            InitializeStepSize(totalCapacity);

            previousHitRate = 1.0;
        }

        public int Window => this.windowCapacity;

        public int Protected => this.protectedCapacity;

        public int Probation => this.probationCapacity;

        public int Capacity => this.max;

        // Apply changes to the ratio of window to main, window = recency-biased, main = frequency-biased.
        public void OptimizePartitioning2(ICacheMetrics metrics, int sampleThreshold)
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

            double hitRateChange = previousHitRate - sampleHitRate;
            double amount = (hitRateChange >= 0) ? stepSize : -stepSize;
            double nextStepSize = (Math.Abs(hitRateChange) >= HillClimberRestartThreshold)
                ? HillClimberStepPercent * Capacity * (amount >= 0 ? 1 : -1)
                : HillClimberStepDecayRate * amount;

            stepSize = nextStepSize;

            previousHitCount = newHits;
            previousMissCount = newMisses;
            previousHitRate = sampleHitRate;

            // amount is actually how much to increment/decrement the window, expressed as a fraction of capacity
            //Adjust(amount);


            // 1.0625 = 100 + 6.25 / 100
            double x = (100 + amount) / 100.0;

            // 0.0625

            mainRatio *= x;
            mainRatio = Clamp(mainRatio, MinMainPercentage, MaxMainPercentage);

            (windowCapacity, protectedCapacity, probationCapacity) = ComputeQueueCapacity(max, mainRatio);
        }

        private void InitializeStepSize2(int cacheSize)
        {
            stepSize = -HillClimberStepPercent * cacheSize;
        }

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

        private void InitializeStepSize(int cacheSize)
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
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than or equal to 3.");
            }

            int window = capacity - (int)(mainPercentage * capacity);
            int mainProtected = (int)(0.8 * (capacity - window));
            int mainProbation = capacity - window - mainProtected;

            return (window, mainProtected, mainProbation);
        }
    }
}
