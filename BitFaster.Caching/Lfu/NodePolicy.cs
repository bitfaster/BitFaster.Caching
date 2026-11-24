using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lfu
{
    internal interface INodePolicy<K, V, N, E>
        where K : notnull
        where N : LfuNode<K, V>
        where E : struct, IEventPolicy<K, V>
    {
        LfuEvictionPolicy<K, V> Eviction { get; }
        IExpiryCalculator<K, V>? ExpiryCalculator { get; }
        void Initialize(int capacity);
        N Create(K key, V value);
        bool UpdateValue(N node, V value);
        bool IsExpired(N node);
        void OnRead(N node);
        void OnWrite(N node);
        void AfterRead(N node);
        bool AfterWrite<P>(ref ConcurrentLfuCore<K, V, N, P, E> cache, N node) where P : struct, INodePolicy<K, V, N, E>;
        void OnEvict(N node);
        void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, N, P, E> cache) where P : struct, INodePolicy<K, V, N, E>;
        void EvictEntries<P>(ref ConcurrentLfuCore<K, V, N, P, E> cache, CmSketch<K> sketch, ItemRemovedReason reason) where P : struct, INodePolicy<K, V, N, E>;
        void OptimizePartitioning(ICacheMetrics metrics, int sampleThreshold);
    }

    internal struct AccessOrderPolicy<K, V, E> : INodePolicy<K, V, AccessOrderNode<K, V>, E>
        where K : notnull
        where E : struct, IEventPolicy<K, V>
    {
        internal UnweightedEvictionPolicy<K, V> eviction;

        public LfuEvictionPolicy<K, V> Eviction => this.eviction;

        public IExpiryCalculator<K, V>? ExpiryCalculator => null;

        public void Initialize(int capacity)
        {
            this.eviction = new UnweightedEvictionPolicy<K, V>(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AccessOrderNode<K, V> Create(K key, V value)
        {
            return new AccessOrderNode<K, V>(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UpdateValue(AccessOrderNode<K, V> node, V value)
        {
            node.Value = value;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired(AccessOrderNode<K, V> node)
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRead(AccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnWrite(AccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterRead(AccessOrderNode<K, V> node)
        {
            this.eviction.OnAccess(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AfterWrite<P>(ref ConcurrentLfuCore<K, V, AccessOrderNode<K, V>, P, E> cache, AccessOrderNode<K, V> node)
            where P : struct, INodePolicy<K, V, AccessOrderNode<K, V>, E>
        {
            return this.eviction.OnWrite(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnEvict(AccessOrderNode<K, V> node)
        {
            UnweightedEvictionPolicy<K, V>.Remove(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, AccessOrderNode<K, V>, P, E> cache) where P : struct, INodePolicy<K, V, AccessOrderNode<K, V>, E>
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EvictEntries<P>(ref ConcurrentLfuCore<K, V, AccessOrderNode<K, V>, P, E> cache, CmSketch<K> sketch, ItemRemovedReason reason)
            where P : struct, INodePolicy<K, V, AccessOrderNode<K, V>, E>
        {
            this.eviction.EvictEntries(ref cache, sketch, reason);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OptimizePartitioning(ICacheMetrics metrics, int sampleThreshold)
        {
            this.eviction.OptimizePartitioning(metrics, sampleThreshold);
        }
    }

    internal struct ExpireAfterPolicy<K, V, E> : INodePolicy<K, V, TimeOrderNode<K, V>, E>
        where K : notnull
        where E : struct, IEventPolicy<K, V>
    {
        internal UnweightedEvictionPolicy<K, V> eviction;
        private ExpiryPolicy<K, V> expiry;

        public ExpireAfterPolicy(IExpiryCalculator<K, V> expiryCalculator)
        {
            this.eviction = null!;
            this.expiry = new ExpiryPolicy<K, V>(expiryCalculator);
        }

        public LfuEvictionPolicy<K, V> Eviction => this.eviction;

        public IExpiryCalculator<K, V> ExpiryCalculator => this.expiry.Calculator;

        public void Initialize(int capacity)
        {
            this.eviction = new UnweightedEvictionPolicy<K, V>(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeOrderNode<K, V> Create(K key, V value)
        {
            return new TimeOrderNode<K, V>(key, value) { TimeToExpire = this.expiry.GetExpireAfterCreate(key, value) };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UpdateValue(TimeOrderNode<K, V> node, V value)
        {
            node.Value = value;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired(TimeOrderNode<K, V> node)
        {
            return this.expiry.IsExpired(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRead(TimeOrderNode<K, V> node)
        {
            this.expiry.OnRead(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnWrite(TimeOrderNode<K, V> node)
        {
            this.expiry.OnWrite(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterRead(TimeOrderNode<K, V> node)
        {
            this.eviction.OnAccess(node);
            this.expiry.AfterRead(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AfterWrite<P>(ref ConcurrentLfuCore<K, V, TimeOrderNode<K, V>, P, E> cache, TimeOrderNode<K, V> node)
            where P : struct, INodePolicy<K, V, TimeOrderNode<K, V>, E>
        {
            bool updated = this.eviction.OnWrite(node);
            this.expiry.AfterWrite(node);
            return updated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnEvict(TimeOrderNode<K, V> node)
        {
            UnweightedEvictionPolicy<K, V>.Remove(node);
            TimerWheel<K, V>.Deschedule(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, TimeOrderNode<K, V>, P, E> cache) where P : struct, INodePolicy<K, V, TimeOrderNode<K, V>, E>
        {
            this.expiry.ExpireEntries(ref cache);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EvictEntries<P>(ref ConcurrentLfuCore<K, V, TimeOrderNode<K, V>, P, E> cache, CmSketch<K> sketch, ItemRemovedReason reason)
            where P : struct, INodePolicy<K, V, TimeOrderNode<K, V>, E>
        {
            this.eviction.EvictEntries(ref cache, sketch, reason);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OptimizePartitioning(ICacheMetrics metrics, int sampleThreshold)
        {
            this.eviction.OptimizePartitioning(metrics, sampleThreshold);
        }
    }

    internal struct ExpiryPolicy<K, V>
        where K : notnull
    {
        private readonly TimerWheel<K, V> wheel;
        private Duration current;

        internal ExpiryPolicy(IExpiryCalculator<K, V> calculator)
        {
            this.Calculator = calculator;
            this.current = Duration.SinceEpoch();
            this.wheel = new TimerWheel<K, V> { time = current.raw };
        }

        internal IExpiryCalculator<K, V> Calculator { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Duration GetExpireAfterCreate(K key, V value)
        {
            var duration = this.Calculator.GetExpireAfterCreate(key, value);
            return Duration.SinceEpoch() + duration;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsExpired(TimeOrderNode<K, V> node)
        {
            this.current = Duration.SinceEpoch();
            return node.TimeToExpire < this.current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnRead(TimeOrderNode<K, V> node)
        {
            // IsExpired immediately precedes OnRead, so reuse its clock sample.
            node.TimeToExpire = current + this.Calculator.GetExpireAfterRead(node.Key, node.Value, node.TimeToExpire - current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnWrite(TimeOrderNode<K, V> node)
        {
            var now = Duration.SinceEpoch();
            node.TimeToExpire = now + this.Calculator.GetExpireAfterUpdate(node.Key, node.Value, node.TimeToExpire - now);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AfterRead(TimeOrderNode<K, V> node)
        {
            this.wheel.Reschedule(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AfterWrite(TimeOrderNode<K, V> node)
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
        internal void ExpireEntries<N, P, E>(ref ConcurrentLfuCore<K, V, N, P, E> cache)
            where N : LfuNode<K, V>
            where P : struct, INodePolicy<K, V, N, E>
            where E : struct, IEventPolicy<K, V>
        {
            wheel.Advance<N, P, N, E>(ref cache, Duration.SinceEpoch());
        }
    }
}
