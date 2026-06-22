using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// A weighted node policy for caches without expiry. Each entry is weighed using the supplied
    /// <see cref="IWeigher{K, V}"/>, and the cache is bounded by total weight.
    /// </summary>
    internal struct WeightedAccessOrderPolicy<K, V, E> : INodePolicy<K, V, WeightedAccessOrderNode<K, V>, E>
        where K : notnull
        where E : struct, IEventPolicy<K, V>
    {
        private readonly IWeigher<K, V> weigher;

        public WeightedAccessOrderPolicy(IWeigher<K, V> weigher)
        {
            this.weigher = weigher;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WeightedAccessOrderNode<K, V> Create(K key, V value)
        {
            return new WeightedAccessOrderNode<K, V>(key, value) { Weight = Weigh(key, value) };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired(WeightedAccessOrderNode<K, V> node)
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRead(WeightedAccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnWrite(WeightedAccessOrderNode<K, V> node)
        {
            // the value may have changed, recompute the weight synchronously while the node is locked
            node.Weight = Weigh(node.Key, node.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterRead(WeightedAccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterWrite(WeightedAccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnEvict(WeightedAccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, WeightedAccessOrderNode<K, V>, P, E> cache) where P : struct, INodePolicy<K, V, WeightedAccessOrderNode<K, V>, E>
        {
        }

        public bool IsWeighted => true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetWeight(LfuNode<K, V> node) => ((WeightedAccessOrderNode<K, V>)node).Weight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPolicyWeight(LfuNode<K, V> node) => ((WeightedAccessOrderNode<K, V>)node).PolicyWeight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPolicyWeight(LfuNode<K, V> node, int weight) => ((WeightedAccessOrderNode<K, V>)node).PolicyWeight = weight;

        public IExpiryCalculator<K, V>? ExpiryCalculator => null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Weigh(K key, V value)
        {
            int weight = weigher.Weigh(key, value);

            if (weight < 0)
                Throw.ArgOutOfRange(nameof(weight), "Weigher must return a non-negative weight.");

            return weight;
        }
    }

    /// <summary>
    /// A weighted node policy for caches with expiry. Each entry is weighed using the supplied
    /// <see cref="IWeigher{K, V}"/> and expires using the supplied <see cref="IExpiryCalculator{K, V}"/>.
    /// </summary>
    internal struct WeightedExpireAfterPolicy<K, V, E> : INodePolicy<K, V, WeightedTimeOrderNode<K, V>, E>
        where K : notnull
        where E : struct, IEventPolicy<K, V>
    {
        private readonly IWeigher<K, V> weigher;
        private readonly IExpiryCalculator<K, V> expiryCalculator;
        private readonly TimerWheel<K, V> wheel;
        private Duration current;

        public WeightedExpireAfterPolicy(IWeigher<K, V> weigher, IExpiryCalculator<K, V> expiryCalculator)
        {
            this.wheel = new TimerWheel<K, V>();
            this.weigher = weigher;
            this.expiryCalculator = expiryCalculator;
            this.current = Duration.SinceEpoch();
            this.wheel.time = current.raw;
        }

        public IExpiryCalculator<K, V> ExpiryCalculator => expiryCalculator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WeightedTimeOrderNode<K, V> Create(K key, V value)
        {
            var expiry = expiryCalculator.GetExpireAfterCreate(key, value);
            return new WeightedTimeOrderNode<K, V>(key, value) { TimeToExpire = Duration.SinceEpoch() + expiry, Weight = Weigh(key, value) };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired(WeightedTimeOrderNode<K, V> node)
        {
            current = Duration.SinceEpoch();
            return node.TimeToExpire < current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRead(WeightedTimeOrderNode<K, V> node)
        {
            node.TimeToExpire = current + expiryCalculator.GetExpireAfterRead(node.Key, node.Value, node.TimeToExpire - current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnWrite(WeightedTimeOrderNode<K, V> node)
        {
            var c = Duration.SinceEpoch();
            node.TimeToExpire = c + expiryCalculator.GetExpireAfterUpdate(node.Key, node.Value, node.TimeToExpire - c);
            node.Weight = Weigh(node.Key, node.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterRead(WeightedTimeOrderNode<K, V> node)
        {
            wheel.Reschedule(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterWrite(WeightedTimeOrderNode<K, V> node)
        {
            // if the node is not yet scheduled, it is being created
            // the time is set on create in case it is read before the buffer is processed
            if (node.GetNextInTimeOrder() == null)
            {
                wheel.Schedule(node);
            }
            else
            {
                wheel.Reschedule(node);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnEvict(WeightedTimeOrderNode<K, V> node)
        {
            TimerWheel<K, V>.Deschedule(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, WeightedTimeOrderNode<K, V>, P, E> cache) where P : struct, INodePolicy<K, V, WeightedTimeOrderNode<K, V>, E>
        {
            wheel.Advance<WeightedTimeOrderNode<K, V>, P, WeightedTimeOrderNode<K, V>, E>(ref cache, Duration.SinceEpoch());
        }

        public bool IsWeighted => true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetWeight(LfuNode<K, V> node) => ((WeightedTimeOrderNode<K, V>)node).Weight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPolicyWeight(LfuNode<K, V> node) => ((WeightedTimeOrderNode<K, V>)node).PolicyWeight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPolicyWeight(LfuNode<K, V> node, int weight) => ((WeightedTimeOrderNode<K, V>)node).PolicyWeight = weight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Weigh(K key, V value)
        {
            int weight = weigher.Weigh(key, value);

            if (weight < 0)
                Throw.ArgOutOfRange(nameof(weight), "Weigher must return a non-negative weight.");

            return weight;
        }
    }
}
