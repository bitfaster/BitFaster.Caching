using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lfu
{
    internal sealed class UnweightedEvictionPolicy<K, V> : LfuEvictionPolicy<K, V>
        where K : notnull
    {
        internal UnweightedEvictionPolicy(int capacity)
            : base(new LfuCapacityPartition(capacity))
        {
        }

        internal void OnAccess(LfuNode<K, V> node)
        {
            switch (node.Position)
            {
                case Position.Window:
                    Debug.Assert(node.list == this.windowLru);
                    this.windowLru.MoveToEnd(node);
                    break;
                case Position.Probation:
                    Debug.Assert(node.list == this.probationLru);
                    PromoteProbation(node);
                    break;
                case Position.Protected:
                    Debug.Assert(node.list == this.protectedLru);
                    this.protectedLru.MoveToEnd(node);
                    break;
            }
        }

        internal bool OnWrite(LfuNode<K, V> node)
        {
            switch (node.Position)
            {
                case Position.Window:
                    if (node.list == null)
                    {
                        this.windowLru.AddLast(node);
                        return false;
                    }

                    Debug.Assert(node.list == this.windowLru);
                    this.windowLru.MoveToEnd(node);
                    break;
                case Position.Probation:
                    Debug.Assert(node.list == this.probationLru);
                    PromoteProbation(node);
                    break;
                case Position.Protected:
                    Debug.Assert(node.list == this.protectedLru);
                    this.protectedLru.MoveToEnd(node);
                    break;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Remove(LfuNode<K, V> node)
        {
            node.list?.Remove(node);
        }

        internal void EvictEntries<N, P, E>(ref ConcurrentLfuCore<K, V, N, P, E> cache, CmSketch<K> sketch, ItemRemovedReason reason)
            where N : LfuNode<K, V>
            where P : struct, INodePolicy<K, V, N, E>
            where E : struct, IEventPolicy<K, V>
        {
            var candidate = EvictFromWindow();
            EvictFromMain(ref cache, sketch, candidate, reason);
        }

        internal void OptimizePartitioning(ICacheMetrics metrics, int sampleThreshold)
        {
            this.Capacity.OptimizePartitioning(metrics, sampleThreshold);
            ReFitProtected();
        }

        private void PromoteProbation(LfuNode<K, V> node)
        {
            this.probationLru.Remove(node);
            this.protectedLru.AddLast(node);
            node.Position = Position.Protected;

            if (this.protectedLru.Count > this.Capacity.Protected)
            {
                var demoted = this.protectedLru.First;
                this.protectedLru.RemoveFirst();
                demoted.Position = Position.Probation;
                this.probationLru.AddLast(demoted);
            }
        }

        private LfuNode<K, V>? EvictFromWindow()
        {
            LfuNode<K, V>? first = null;

            while (this.windowLru.Count > this.Capacity.Window)
            {
                var node = this.windowLru.First;
                this.windowLru.RemoveFirst();
                first ??= node;
                this.probationLru.AddLast(node);
                node.Position = Position.Probation;
            }

            return first;
        }

        private void EvictFromMain<N, P, E>(ref ConcurrentLfuCore<K, V, N, P, E> cache, CmSketch<K> sketch, LfuNode<K, V>? candidateNode, ItemRemovedReason reason)
            where N : LfuNode<K, V>
            where P : struct, INodePolicy<K, V, N, E>
            where E : struct, IEventPolicy<K, V>
        {
            var victim = new EvictIterator(sketch, this.probationLru.First);
            var candidate = new EvictIterator(sketch, candidateNode);

            while (this.Count > this.Capacity.Capacity)
            {
                if (candidate.node == null | victim.node == null)
                {
                    break;
                }

                if (victim.node == candidate.node)
                {
                    cache.Evict(candidate.node!, reason);
                    break;
                }

                if (candidate.node!.WasRemoved)
                {
                    var evictee = candidate.node;
                    candidate.Next();
                    cache.Evict(evictee, reason);
                    continue;
                }

                if (victim.node!.WasRemoved)
                {
                    var evictee = victim.node;
                    victim.Next();
                    cache.Evict(evictee, reason);
                    continue;
                }

                if (candidate.freq > victim.freq)
                {
                    var evictee = victim.node;
                    victim.Next();
                    candidate.Next();
                    cache.Evict(evictee, reason);
                }
                else
                {
                    var evictee = candidate.node;
                    candidate.Next();
                    cache.Evict(evictee, reason);
                }
            }

            while (this.Count > this.Capacity.Capacity)
            {
                var victim1 = this.probationLru.First;
                var victim2 = victim1.Next;

                if (sketch.EstimateFrequency(victim1.Key) > sketch.EstimateFrequency(victim2.Key))
                {
                    cache.Evict(victim2, reason);
                }
                else
                {
                    cache.Evict(victim1, reason);
                }
            }
        }

        private void ReFitProtected()
        {
            while (this.protectedLru.Count > this.Capacity.Protected)
            {
                var demoted = this.protectedLru.First;
                this.protectedLru.RemoveFirst();
                demoted.Position = Position.Probation;
                this.probationLru.AddLast(demoted);
            }
        }

        private ref struct EvictIterator
        {
            private readonly CmSketch<K> sketch;
            public LfuNode<K, V>? node;
            public int freq;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public EvictIterator(CmSketch<K> sketch, LfuNode<K, V>? node)
            {
                this.sketch = sketch;
                this.node = node;
                freq = node == null ? -1 : sketch.EstimateFrequency(node.Key);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Next()
            {
                node = node!.Next;
                if (node != null)
                {
                    freq = sketch.EstimateFrequency(node.Key);
                }
            }
        }
    }
}
