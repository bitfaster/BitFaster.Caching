using System;
using System.Collections.Generic;
using System.Drawing;
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

        const double HILL_CLIMBER_RESTART_THRESHOLD = 0.05d;
        const double HILL_CLIMBER_STEP_PERCENT = 0.0625d;
        const double HILL_CLIMBER_STEP_DECAY_RATE = 0.98d;

        const double DefaultMainPercentage = 0.99d;

        public LfuCapacityPartition(int totalCapacity)
        {
            this.max = totalCapacity;
            (windowCapacity, protectedCapacity, probationCapacity) = ComputeQueueCapacity(totalCapacity, DefaultMainPercentage);
            InitializeStepSize(totalCapacity);

            previousHitRate = 0.5;
        }

        public int Window => this.windowCapacity;

        public int Protected => this.protectedCapacity;

        public int Probation => this.probationCapacity;

        public int Capacity => this.max;

        public enum PartitionChange
        { 
            None,
            IncreaseWindow,
            DecreaseWindow,
        }

        public void Optimize(ICacheMetrics metrics, int sampleThreshold)
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
            double nextStepSize = (Math.Abs(hitRateChange) >= HILL_CLIMBER_RESTART_THRESHOLD)
                ? HILL_CLIMBER_STEP_PERCENT * Capacity * (amount >= 0 ? 1 : -1)
                : HILL_CLIMBER_STEP_DECAY_RATE * amount;

            stepSize = nextStepSize;

            previousHitCount = newHits;
            previousMissCount = newMisses;
            previousHitRate = sampleHitRate;

            // Apply changes to the ratio of window to main. Window = recency-biased main = frequency-biased.
            // Then in concurrentLfu, move items to preserve queue ratio

            // 6.35
            // 100 - 6.35 = 93.65
            // / 100      =  0.9365
            // * .99      =  0.927135

            // =>

            // 0.0635 starting step size
            // 

            // TODO: this should only adjust sizes of mainprotected and window

            mainRatio += amount;
            (windowCapacity, protectedCapacity, probationCapacity) = ComputeQueueCapacity(max, mainRatio);

            return PartitionChange.None;
        }

        private void InitializeStepSize(int cacheSize)
        {
            stepSize = -HILL_CLIMBER_STEP_PERCENT * cacheSize;
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
