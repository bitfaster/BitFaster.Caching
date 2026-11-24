using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lfu
{
    internal interface INodeWeight<N>
    {
        int GetWeight(N node);
        int GetPolicyWeight(N node);
        void SetPolicyWeight(N node, int weight);
    }

    internal sealed class WeightedEvictionPolicy<K, V, N, W> : LfuEvictionPolicy<K, V>
        where K : notnull
        where N : LfuNode<K, V>
        where W : struct, INodeWeight<N>
    {
        private const int QueueTransferThreshold = 1000;

        private long weightedSize;
        private long windowWeightedSize;
        private long protectedWeightedSize;

        internal WeightedEvictionPolicy(int capacity)
            : base(new LfuCapacityPartition(capacity, weighted: true))
        {
        }

        internal long WeightedSize => this.weightedSize;
        internal long WindowWeightedSize => this.windowWeightedSize;
        internal long ProtectedWeightedSize => this.protectedWeightedSize;

        internal void OnAccess(N node)
        {
            switch (node.Position)
            {
                case Position.Window:
                    this.windowLru.MoveToEnd(node);
                    break;
                case Position.Probation:
                    PromoteProbation(node);
                    break;
                case Position.Protected:
                    this.protectedLru.MoveToEnd(node);
                    break;
            }
        }

        internal bool OnWrite<P, E>(ref ConcurrentLfuCore<K, V, N, P, E> cache, N node)
            where P : struct, INodePolicy<K, V, N, E>
            where E : struct, IEventPolicy<K, V>
        {
            bool updated = node.list != null;
            int weight = default(W).GetWeight(node);
            long delta = weight - GetPolicyWeight(node);
            default(W).SetPolicyWeight(node, weight);
            this.weightedSize += delta;

            switch (node.Position)
            {
                case Position.Window:
                    this.windowWeightedSize += delta;
                    if (!updated)
                    {
                        if (weight > this.Capacity.Window)
                        {
                            this.windowLru.AddFirst(node);
                        }
                        else
                        {
                            this.windowLru.AddLast(node);
                        }
                    }
                    else if (weight > this.Capacity.Window)
                    {
                        this.windowLru.MoveToFront(node);
                    }
                    else
                    {
                        this.windowLru.MoveToEnd(node);
                    }
                    break;
                case Position.Probation:
                    if (weight <= this.Capacity.Capacity)
                    {
                        PromoteProbation(node);
                    }
                    break;
                case Position.Protected:
                    this.protectedWeightedSize += delta;
                    this.protectedLru.MoveToEnd(node);
                    break;
            }

            if (weight > this.Capacity.Capacity)
            {
                cache.Evict(node, ItemRemovedReason.Evicted);
            }

            return updated;
        }

        internal void Remove(N node)
        {
            if (node.list == null)
            {
                return;
            }

            int weight = GetPolicyWeight(node);
            this.weightedSize -= weight;
            if (node.Position == Position.Window)
            {
                this.windowWeightedSize -= weight;
            }
            else if (node.Position == Position.Protected)
            {
                this.protectedWeightedSize -= weight;
            }

            default(W).SetPolicyWeight(node, 0);
            node.list.Remove(node);
        }

        internal void EvictEntries<P, E>(ref ConcurrentLfuCore<K, V, N, P, E> cache, CmSketch<K> sketch, ItemRemovedReason reason)
            where P : struct, INodePolicy<K, V, N, E>
            where E : struct, IEventPolicy<K, V>
        {
            var candidate = EvictFromWindow();
            EvictFromMain(ref cache, sketch, candidate, reason);
        }

        internal void OptimizePartitioning(ICacheMetrics metrics, int sampleThreshold)
        {
            int adjustment = this.Capacity.DetermineWeightedAdjustment(metrics, sampleThreshold);
            if (adjustment > 0)
            {
                IncreaseWindow(adjustment);
            }
            else if (adjustment < 0)
            {
                DecreaseWindow(adjustment);
            }

            ReFitProtected();
        }

        // W is a struct so these accessors specialize to the concrete node layout, without virtual node members.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPolicyWeight(LfuNode<K, V> node)
        {
            return default(W).GetPolicyWeight((N)node);
        }

        private void PromoteProbation(LfuNode<K, V> node)
        {
            int weight = GetPolicyWeight(node);
            if (weight > this.Capacity.Protected)
            {
                this.probationLru.MoveToEnd(node);
                return;
            }

            this.probationLru.Remove(node);
            this.protectedLru.AddLast(node);
            node.Position = Position.Protected;
            this.protectedWeightedSize += weight;
            ReFitProtected();
        }

        private LfuNode<K, V>? EvictFromWindow()
        {
            LfuNode<K, V>? first = null;
            var node = this.windowLru.First;

            while (this.windowWeightedSize > this.Capacity.Window && node != null)
            {
                var next = node.Next;
                int weight = GetPolicyWeight(node);
                if (weight != 0)
                {
                    first ??= node;
                    this.windowLru.Remove(node);
                    this.probationLru.AddLast(node);
                    node.Position = Position.Probation;
                    this.windowWeightedSize -= weight;
                }

                node = next;
            }

            return first;
        }

        private void EvictFromMain<P, E>(ref ConcurrentLfuCore<K, V, N, P, E> cache, CmSketch<K> sketch, LfuNode<K, V>? candidate, ItemRemovedReason reason)
            where P : struct, INodePolicy<K, V, N, E>
            where E : struct, IEventPolicy<K, V>
        {
            var victim = this.probationLru.First;
            var victimQueue = Position.Probation;
            var candidateQueue = Position.Probation;

            while (this.weightedSize > this.Capacity.Capacity)
            {
                if (candidate == null && candidateQueue == Position.Probation)
                {
                    candidate = this.windowLru.First;
                    candidateQueue = Position.Window;
                }

                if (candidate == null && victim == null)
                {
                    if (victimQueue == Position.Probation)
                    {
                        victim = this.protectedLru.First;
                        victimQueue = Position.Protected;
                        continue;
                    }

                    if (victimQueue == Position.Protected)
                    {
                        victim = this.windowLru.First;
                        victimQueue = Position.Window;
                        continue;
                    }

                    break;
                }

                if (victim != null && GetPolicyWeight(victim) == 0)
                {
                    victim = victim.Next;
                    continue;
                }

                if (candidate != null && GetPolicyWeight(candidate) == 0)
                {
                    candidate = candidate.Next;
                    continue;
                }

                if (victim == null)
                {
                    var evictee = candidate!;
                    candidate = candidate!.Next;
                    cache.Evict(evictee, reason);
                    continue;
                }

                if (candidate == null)
                {
                    var evictee = victim;
                    victim = victim.Next;
                    cache.Evict(evictee, reason);
                    continue;
                }

                if (candidate == victim)
                {
                    victim = victim.Next;
                    cache.Evict(candidate, reason);
                    candidate = null;
                    continue;
                }

                if (victim.WasRemoved)
                {
                    var evictee = victim;
                    victim = victim.Next;
                    cache.Evict(evictee, reason);
                    continue;
                }

                if (candidate.WasRemoved || GetPolicyWeight(candidate) > this.Capacity.Capacity)
                {
                    var evictee = candidate;
                    candidate = candidate.Next;
                    cache.Evict(evictee, reason);
                    continue;
                }

                if (sketch.EstimateFrequency(candidate.Key) > sketch.EstimateFrequency(victim.Key))
                {
                    var evictee = victim;
                    victim = victim.Next;
                    candidate = candidate.Next;
                    cache.Evict(evictee, reason);
                }
                else
                {
                    var evictee = candidate;
                    candidate = candidate.Next;
                    cache.Evict(evictee, reason);
                }
            }
        }

        private void ReFitProtected()
        {
            while (this.protectedWeightedSize > this.Capacity.Protected)
            {
                var demoted = this.protectedLru.First;
                this.protectedLru.RemoveFirst();
                this.protectedWeightedSize -= GetPolicyWeight(demoted);
                demoted.Position = Position.Probation;
                this.probationLru.AddLast(demoted);
            }
        }

        private void IncreaseWindow(int adjustment)
        {
            int quota = this.Capacity.ResizeWindow(adjustment);
            if (quota == 0)
            {
                return;
            }

            ReFitProtected();

            for (int i = 0; i < QueueTransferThreshold; i++)
            {
                var candidate = this.probationLru.First;
                bool probation = true;
                if (candidate == null || quota < GetPolicyWeight(candidate))
                {
                    candidate = this.protectedLru.First;
                    probation = false;
                }

                if (candidate == null)
                {
                    break;
                }

                int weight = GetPolicyWeight(candidate);
                if (quota < weight)
                {
                    break;
                }

                quota -= weight;
                if (probation)
                {
                    this.probationLru.Remove(candidate);
                }
                else
                {
                    this.protectedWeightedSize -= weight;
                    this.protectedLru.Remove(candidate);
                }

                this.windowWeightedSize += weight;
                this.windowLru.AddLast(candidate);
                candidate.Position = Position.Window;
            }

            this.Capacity.ResizeWindow(-quota);
        }

        private void DecreaseWindow(int adjustment)
        {
            int quota = -this.Capacity.ResizeWindow(adjustment);
            if (quota == 0)
            {
                return;
            }

            for (int i = 0; i < QueueTransferThreshold; i++)
            {
                var candidate = this.windowLru.First;
                if (candidate == null)
                {
                    break;
                }

                int weight = GetPolicyWeight(candidate);
                if (quota < weight)
                {
                    break;
                }

                quota -= weight;
                this.windowWeightedSize -= weight;
                this.windowLru.Remove(candidate);
                this.probationLru.AddLast(candidate);
                candidate.Position = Position.Probation;
            }

            this.Capacity.ResizeWindow(quota);
        }
    }
}
