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
        void AdvanceTime();
        void OnRead(N node);
        void OnWrite(N node);
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
        public void AdvanceTime()
        {
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
            this.current = Duration.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeOrderNode<K, V> Create(K key, V value)
        {
            var expiry = expiryCalculator.GetExpireAfterCreate(key, value);
            return new TimeOrderNode<K, V>(key, value) { TimeToExpire = Duration.SinceEpoch() + expiry };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired(TimeOrderNode<K, V> node)
        {
            return node.TimeToExpire < Duration.SinceEpoch();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTime()
        {
            current = Duration.SinceEpoch();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRead(TimeOrderNode<K, V> node)
        {
            var currentExpiry = node.TimeToExpire - current;
            node.TimeToExpire = current + expiryCalculator.GetExpireAfterRead(node.Key, node.Value, currentExpiry);
            wheel.Reschedule(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnWrite(TimeOrderNode<K, V> node)
        {
            // if the node is not yet scheduled, it is being created
            // the time is set on create in case it is read before the buffer is processed
            if (node.GetNextInTimeOrder() == null)
            {
                wheel.Schedule(node);
            }
            else
            {
                var currentExpiry = node.TimeToExpire - current;
                node.TimeToExpire = current + expiryCalculator.GetExpireAfterUpdate(node.Key, node.Value, currentExpiry);
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
            wheel.Advance(ref cache, current);
        }
    }
}
