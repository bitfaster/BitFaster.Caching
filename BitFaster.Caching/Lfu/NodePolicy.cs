using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Lfu
{
    internal interface INodePolicy<K, V, N>
        where K : notnull
        where N : LfuNode<K, V>
    {
        N Create(K key, V value);
        bool IsExpired(N node);
        void OnRead(N node);
        void OnWrite(N node);
        void AfterRead(N node);
        void AfterWrite(N node);
        void OnEvict(N node);
        void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, N, P> cache) where P : struct, INodePolicy<K, V, N>;
    }

    internal struct AccessOrderPolicy<K, V> : INodePolicy<K, V, AccessOrderNode<K, V>>
        where K : notnull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AccessOrderNode<K, V> Create(K key, V value)
        {
            return new AccessOrderNode<K, V>(key, value);
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterWrite(AccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnEvict(AccessOrderNode<K, V> node)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, AccessOrderNode<K, V>, P> cache) where P : struct, INodePolicy<K, V, AccessOrderNode<K, V>>
        {
        }
    }

    internal struct ExpireAfterPolicy<K, V> : INodePolicy<K, V, TimeOrderNode<K, V>>
        where K : notnull
    {
        private readonly IExpiryCalculator<K, V> expiryCalculator;
        private readonly TimerWheel<K, V> wheel;
        private Duration current;

        public ExpireAfterPolicy(IExpiryCalculator<K, V> expiryCalculator)
        {
            this.wheel = new TimerWheel<K, V>();
            this.expiryCalculator = expiryCalculator;
            this.current = Duration.SinceEpoch();
            this.wheel.time = current.raw;
        }

        public IExpiryCalculator<K, V> ExpiryCalculator => expiryCalculator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeOrderNode<K, V> Create(K key, V value)
        {
            var expiry = expiryCalculator.GetExpireAfterCreate(key, value);
            return new TimeOrderNode<K, V>(key, value) { TimeToExpire = Duration.SinceEpoch() + expiry };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired(TimeOrderNode<K, V> node)
        {
            current = Duration.SinceEpoch();
            return node.TimeToExpire < current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRead(TimeOrderNode<K, V> node)
        {
            // we know IsExpired is always called immediate before OnRead, so piggyback on the current time
            node.TimeToExpire = current + expiryCalculator.GetExpireAfterRead(node.Key, node.Value, node.TimeToExpire - current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnWrite(TimeOrderNode<K, V> node)
        {
            var current = Duration.SinceEpoch();
            node.TimeToExpire = current + expiryCalculator.GetExpireAfterUpdate(node.Key, node.Value, node.TimeToExpire - current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterRead(TimeOrderNode<K, V> node)
        {
            wheel.Reschedule(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AfterWrite(TimeOrderNode<K, V> node)
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
        public void OnEvict(TimeOrderNode<K, V> node)
        {
            TimerWheel<K, V>.Deschedule(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExpireEntries<P>(ref ConcurrentLfuCore<K, V, TimeOrderNode<K, V>, P> cache) where P : struct, INodePolicy<K, V, TimeOrderNode<K, V>>
        {
            wheel.Advance(ref cache, Duration.SinceEpoch());
        }
    }
}
