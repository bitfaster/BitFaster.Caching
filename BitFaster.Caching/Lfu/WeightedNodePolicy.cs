using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lfu
{
    internal struct WeightedAccessOrderPolicy<K, V, E> : INodePolicy<K, V, WeightedAccessOrderNode<K, V>, E>, INodeWeight<WeightedAccessOrderNode<K, V>>
        where K : notnull
        where E : struct, IEventPolicy<K, V>
    {
        private readonly IWeightCalculator<K, V> weigher;
        internal WeightedEvictionPolicy<K, V, WeightedAccessOrderNode<K, V>, WeightedAccessOrderPolicy<K, V, E>> eviction;

        internal WeightedAccessOrderPolicy(IWeightCalculator<K, V> weigher)
        {
            this.weigher = weigher;
            this.eviction = null!;
        }

        public LfuEvictionPolicy<K, V> Eviction => this.eviction;
        public IExpiryCalculator<K, V>? ExpiryCalculator => null;

        public void Initialize(int capacity)
        {
            this.eviction = new WeightedEvictionPolicy<K, V, WeightedAccessOrderNode<K, V>, WeightedAccessOrderPolicy<K, V, E>>(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WeightedAccessOrderNode<K, V> Create(K key, V value)
        {
            return new WeightedAccessOrderNode<K, V>(key, value, WeightCalculator.GetWeight(this.weigher, key, value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UpdateValue(WeightedAccessOrderNode<K, V> node, V value)
        {
            int weight = WeightCalculator.GetWeight(this.weigher, node.Key, value);
            bool changed = weight != node.Weight;
            node.Value = value;
            node.Weight = weight;
            return changed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired(WeightedAccessOrderNode<K, V> node) => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRead(WeightedAccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnWrite(WeightedAccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterRead(WeightedAccessOrderNode<K, V> node)
        {
            this.eviction.OnAccess(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AfterWrite<P>(ref ConcurrentLfuCore<K, V, WeightedAccessOrderNode<K, V>, P, E> cache, WeightedAccessOrderNode<K, V> node)
            where P : struct, INodePolicy<K, V, WeightedAccessOrderNode<K, V>, E>
        {
            return this.eviction.OnWrite(ref cache, node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnEvict(WeightedAccessOrderNode<K, V> node)
        {
            this.eviction.Remove(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, WeightedAccessOrderNode<K, V>, P, E> cache)
            where P : struct, INodePolicy<K, V, WeightedAccessOrderNode<K, V>, E>
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EvictEntries<P>(ref ConcurrentLfuCore<K, V, WeightedAccessOrderNode<K, V>, P, E> cache, CmSketch<K> sketch, ItemRemovedReason reason)
            where P : struct, INodePolicy<K, V, WeightedAccessOrderNode<K, V>, E>
        {
            this.eviction.EvictEntries(ref cache, sketch, reason);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OptimizePartitioning(ICacheMetrics metrics, int sampleThreshold)
        {
            this.eviction.OptimizePartitioning(metrics, sampleThreshold);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetWeight(WeightedAccessOrderNode<K, V> node) => node.Weight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPolicyWeight(WeightedAccessOrderNode<K, V> node) => node.PolicyWeight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPolicyWeight(WeightedAccessOrderNode<K, V> node, int weight) => node.PolicyWeight = weight;
    }

    internal struct WeightedExpireAfterPolicy<K, V, E> : INodePolicy<K, V, WeightedTimeOrderNode<K, V>, E>, INodeWeight<WeightedTimeOrderNode<K, V>>
        where K : notnull
        where E : struct, IEventPolicy<K, V>
    {
        private readonly IWeightCalculator<K, V> weigher;
        private ExpiryPolicy<K, V> expiry;
        internal WeightedEvictionPolicy<K, V, WeightedTimeOrderNode<K, V>, WeightedExpireAfterPolicy<K, V, E>> eviction;

        internal WeightedExpireAfterPolicy(IWeightCalculator<K, V> weigher, IExpiryCalculator<K, V> expiryCalculator)
        {
            this.weigher = weigher;
            this.expiry = new ExpiryPolicy<K, V>(expiryCalculator);
            this.eviction = null!;
        }

        public LfuEvictionPolicy<K, V> Eviction => this.eviction;
        public IExpiryCalculator<K, V> ExpiryCalculator => this.expiry.Calculator;

        public void Initialize(int capacity)
        {
            this.eviction = new WeightedEvictionPolicy<K, V, WeightedTimeOrderNode<K, V>, WeightedExpireAfterPolicy<K, V, E>>(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WeightedTimeOrderNode<K, V> Create(K key, V value)
        {
            int weight = WeightCalculator.GetWeight(this.weigher, key, value);
            return new WeightedTimeOrderNode<K, V>(key, value, weight) { TimeToExpire = this.expiry.GetExpireAfterCreate(key, value) };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UpdateValue(WeightedTimeOrderNode<K, V> node, V value)
        {
            int weight = WeightCalculator.GetWeight(this.weigher, node.Key, value);
            bool changed = weight != node.Weight;
            node.Value = value;
            node.Weight = weight;
            return changed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired(WeightedTimeOrderNode<K, V> node) => this.expiry.IsExpired(node);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRead(WeightedTimeOrderNode<K, V> node) => this.expiry.OnRead(node);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnWrite(WeightedTimeOrderNode<K, V> node) => this.expiry.OnWrite(node);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterRead(WeightedTimeOrderNode<K, V> node)
        {
            this.eviction.OnAccess(node);
            this.expiry.AfterRead(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AfterWrite<P>(ref ConcurrentLfuCore<K, V, WeightedTimeOrderNode<K, V>, P, E> cache, WeightedTimeOrderNode<K, V> node)
            where P : struct, INodePolicy<K, V, WeightedTimeOrderNode<K, V>, E>
        {
            bool updated = this.eviction.OnWrite(ref cache, node);
            if (!node.WasRemoved)
            {
                this.expiry.AfterWrite(node);
            }

            return updated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnEvict(WeightedTimeOrderNode<K, V> node)
        {
            this.eviction.Remove(node);
            TimerWheel<K, V>.Deschedule(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, WeightedTimeOrderNode<K, V>, P, E> cache)
            where P : struct, INodePolicy<K, V, WeightedTimeOrderNode<K, V>, E>
        {
            this.expiry.ExpireEntries(ref cache);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EvictEntries<P>(ref ConcurrentLfuCore<K, V, WeightedTimeOrderNode<K, V>, P, E> cache, CmSketch<K> sketch, ItemRemovedReason reason)
            where P : struct, INodePolicy<K, V, WeightedTimeOrderNode<K, V>, E>
        {
            this.eviction.EvictEntries(ref cache, sketch, reason);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OptimizePartitioning(ICacheMetrics metrics, int sampleThreshold)
        {
            this.eviction.OptimizePartitioning(metrics, sampleThreshold);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetWeight(WeightedTimeOrderNode<K, V> node) => node.Weight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPolicyWeight(WeightedTimeOrderNode<K, V> node) => node.PolicyWeight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPolicyWeight(WeightedTimeOrderNode<K, V> node, int weight) => node.PolicyWeight = weight;
    }

    internal static class WeightCalculator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetWeight<K, V>(IWeightCalculator<K, V> calculator, K key, V value)
        {
            int weight = calculator.GetWeight(key, value);
            if (weight < 0)
                Throw.ArgOutOfRange(nameof(weight), "Weight calculator must return a non-negative weight.");

            return weight;
        }
    }
}
