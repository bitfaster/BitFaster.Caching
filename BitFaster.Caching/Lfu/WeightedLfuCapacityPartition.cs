using System;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// Represents the weighted LFU capacity partition. Holds the window/main weighted maximums and uses
    /// a hill climbing algorithm to optimize the partition sizes (in weight units) over time.
    /// </summary>
    internal sealed class WeightedLfuCapacityPartition : ICapacityPartition
    {
        private readonly int max;

        private long maximum;
        private long windowMaximum;
        private long mainProtectedMaximum;

        private double stepSize;
        private double previousHitRate;
        private long previousHitCount;
        private long previousMissCount;

        // Weighted eviction tuning, matching Caffeine.
        private const double MainPercentage = 0.99d;
        private const double MainProtectedPercentage = 0.8d;
        internal const int AdmitHashDosThreshold = 6;
        private const double HillClimberStepPercent = 0.0625d;
        private const double HillClimberStepDecay = 0.98d;
        private const double HillClimberRestartThreshold = 0.05d;
        private const double HillClimberMinStep = 2.0d;
        private const long SmallCacheThreshold = 512;
        private const int QueueTransferThreshold = 1000;

        /// <summary>
        /// Initializes a new instance of the WeightedLfuCapacityPartition class with the specified total weight capacity.
        /// </summary>
        /// <param name="totalCapacity">The total weight capacity.</param>
        public WeightedLfuCapacityPartition(int totalCapacity)
        {
            this.max = totalCapacity;

            // Mirror Caffeine's initial split: window ~1% of total weight, protected ~80% of main.
            this.maximum = totalCapacity;
            this.windowMaximum = this.maximum - (long)(MainPercentage * this.maximum);
            this.mainProtectedMaximum = (long)(MainProtectedPercentage * (this.maximum - this.windowMaximum));
            this.previousHitRate = 1.0d;
            double initialStep = Math.Max(HillClimberStepPercent * this.maximum, HillClimberMinStep);
            this.stepSize = (this.maximum <= SmallCacheThreshold) ? initialStep : -initialStep;
        }

        /// <summary>
        /// Gets the total weight capacity.
        /// </summary>
        public int Capacity => this.max;

        /// <summary>
        /// Gets the maximum total weight.
        /// </summary>
        public long Maximum => this.maximum;

        /// <summary>
        /// Gets the maximum weight permitted in the window.
        /// </summary>
        public long WindowMaximum => this.windowMaximum;

        /// <summary>
        /// Gets the maximum weight permitted in the protected space.
        /// </summary>
        public long MainProtectedMaximum => this.mainProtectedMaximum;

        /// <summary>
        /// Adapt the window and main space sizes (in weight units) using a hill climbing algorithm to
        /// iteratively improve hit rate. A larger window favors recency, a larger main favors frequency.
        /// </summary>
        public void OptimizePartitioning<K, V, N, P, E>(ref ConcurrentLfuCore<K, V, N, P, E> cache, ICacheMetrics metrics, int sampleThreshold)
            where K : notnull
            where N : LfuNode<K, V>
            where P : struct, INodePolicy<K, V, N, E>
            where E : struct, IEventPolicy<K, V>
        {
            long adjustment = DetermineWeightedAdjustment(metrics, sampleThreshold);

            if (adjustment > 0)
            {
                IncreaseWindow(ref cache, adjustment);
            }
            else if (adjustment < 0)
            {
                DecreaseWindow(ref cache, -adjustment);
            }
        }

        private long DetermineWeightedAdjustment(ICacheMetrics metrics, int sampleThreshold)
        {
            long newHits = metrics.Hits;
            long newMisses = metrics.Misses;

            long sampleHits = newHits - this.previousHitCount;
            long sampleMisses = newMisses - this.previousMissCount;
            long requestCount = sampleHits + sampleMisses;

            if (requestCount < sampleThreshold)
            {
                return 0;
            }

            double hitRate = (double)sampleHits / requestCount;
            double hitRateChange = hitRate - this.previousHitRate;
            double amount = (hitRateChange >= 0) ? this.stepSize : -this.stepSize;
            double nextStepSize = (Math.Abs(hitRateChange) >= HillClimberRestartThreshold)
                ? CopySign(Math.Max(HillClimberStepPercent * this.maximum, HillClimberMinStep), amount)
                : HillClimberStepDecay * amount;

            this.previousHitRate = hitRate;
            this.previousHitCount = newHits;
            this.previousMissCount = newMisses;
            this.stepSize = nextStepSize;

            return (long)amount;
        }

        private void IncreaseWindow<K, V, N, P, E>(ref ConcurrentLfuCore<K, V, N, P, E> cache, long adjustment)
            where K : notnull
            where N : LfuNode<K, V>
            where P : struct, INodePolicy<K, V, N, E>
            where E : struct, IEventPolicy<K, V>
        {
            if (this.mainProtectedMaximum == 0)
            {
                return;
            }

            long quota = Math.Min(adjustment, this.mainProtectedMaximum);
            this.mainProtectedMaximum -= quota;
            this.windowMaximum += quota;

            cache.ReFitProtectedWeighted();

            for (int i = 0; i < QueueTransferThreshold; i++)
            {
                var candidate = cache.probationLru.First;
                bool probation = true;

                if (candidate == null || quota < cache.policy.GetPolicyWeight(candidate))
                {
                    candidate = cache.protectedLru.First;
                    probation = false;
                }

                if (candidate == null)
                {
                    break;
                }

                int weight = cache.policy.GetPolicyWeight(candidate);
                if (quota < weight)
                {
                    break;
                }

                quota -= weight;

                if (probation)
                {
                    cache.probationLru.Remove(candidate);
                }
                else
                {
                    cache.mainProtectedWeightedSize -= weight;
                    cache.protectedLru.Remove(candidate);
                }

                cache.windowWeightedSize += weight;
                cache.windowLru.AddLast(candidate);
                candidate.Position = Position.Window;
            }

            // return unused quota
            this.mainProtectedMaximum += quota;
            this.windowMaximum -= quota;
        }

        private void DecreaseWindow<K, V, N, P, E>(ref ConcurrentLfuCore<K, V, N, P, E> cache, long adjustment)
            where K : notnull
            where N : LfuNode<K, V>
            where P : struct, INodePolicy<K, V, N, E>
            where E : struct, IEventPolicy<K, V>
        {
            if (this.windowMaximum <= 1)
            {
                return;
            }

            long quota = Math.Min(adjustment, Math.Max(0, this.windowMaximum - 1));
            this.mainProtectedMaximum += quota;
            this.windowMaximum -= quota;

            for (int i = 0; i < QueueTransferThreshold; i++)
            {
                var candidate = cache.windowLru.First;
                if (candidate == null)
                {
                    break;
                }

                int weight = cache.policy.GetPolicyWeight(candidate);
                if (quota < weight)
                {
                    break;
                }

                quota -= weight;

                cache.windowWeightedSize -= weight;
                cache.windowLru.Remove(candidate);
                cache.probationLru.AddLast(candidate);
                candidate.Position = Position.Probation;
            }

            // return unused quota
            this.mainProtectedMaximum -= quota;
            this.windowMaximum += quota;
        }

        private static double CopySign(double magnitude, double sign)
        {
            return (sign < 0) ? -Math.Abs(magnitude) : Math.Abs(magnitude);
        }
    }
}
