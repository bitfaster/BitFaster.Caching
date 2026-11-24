using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu
{
    internal sealed class WeightedConcurrentLfu<K, V, N, P> : ICacheExt<K, V>, IAsyncCacheExt<K, V>, IBoundedPolicy, ITimePolicy, IDiscreteTimePolicy
        where K : notnull
        where N : LfuNode<K, V>
        where P : struct, INodePolicy<K, V, N, EventPolicy<K, V>>
    {
        private ConcurrentLfuCore<K, V, N, P, EventPolicy<K, V>> core;

        internal WeightedConcurrentLfu(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer, P nodePolicy)
        {
            EventPolicy<K, V> eventPolicy = default;
            eventPolicy.SetEventSource(this);
            this.core = new(concurrencyLevel, capacity, scheduler, comparer, () => this.DrainBuffers(), nodePolicy, eventPolicy);
        }

        private void DrainBuffers()
        {
            this.core.DrainBuffers();
        }

        internal ConcurrentLfuCore<K, V, N, P, EventPolicy<K, V>> Core => core;
        internal ref EventPolicy<K, V> EventPolicyRef => ref this.core.eventPolicy;

        public int Count => core.Count;
        public Optional<ICacheMetrics> Metrics => core.Metrics;
        public Optional<ICacheEvents<K, V>> Events => new(new Proxy(this));
        public CachePolicy Policy => CreatePolicy();
        public ICollection<K> Keys => core.Keys;
        public int Capacity => core.Capacity;
        public IScheduler Scheduler => core.Scheduler;

#if NET9_0_OR_GREATER
        public IEqualityComparer<K> Comparer => this.core.Comparer;
#endif

        public void DoMaintenance()
        {
            core.DoMaintenance();
        }

        public void AddOrUpdate(K key, V value)
        {
            core.AddOrUpdate(key, value);
        }

        public void Clear()
        {
            core.Clear();
        }

        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            return core.GetOrAdd(key, valueFactory);
        }

        public V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
#if NET9_0_OR_GREATER
            where TArg : allows ref struct
#endif
        {
            return core.GetOrAdd(key, valueFactory, factoryArgument);
        }

        public ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            return core.GetOrAddAsync(key, valueFactory);
        }

        public ValueTask<V> GetOrAddAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
        {
            return core.GetOrAddAsync(key, valueFactory, factoryArgument);
        }

        public void Trim(int itemCount)
        {
            core.Trim(itemCount);
        }

        public bool TryGet(K key, [MaybeNullWhen(false)] out V value)
        {
            return core.TryGet(key, out value);
        }

        public bool TryRemove(K key)
        {
            return core.TryRemove(key);
        }

        public bool TryRemove(KeyValuePair<K, V> item)
        {
            return core.TryRemove(item);
        }

        public bool TryRemove(K key, [MaybeNullWhen(false)] out V value)
        {
            return core.TryRemove(key, out value);
        }

        public bool TryUpdate(K key, V value)
        {
            return core.TryUpdate(key, value);
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return core.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return core.GetEnumerator();
        }

#if NET9_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAlternateLookup<TAlternateKey, K, V> GetAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            return core.GetAlternateLookup<TAlternateKey>();
        }

        public bool TryGetAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            return core.TryGetAlternateLookup(out lookup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncAlternateLookup<TAlternateKey, K, V> GetAsyncAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            return core.GetAsyncAlternateLookup<TAlternateKey>();
        }

        public bool TryGetAsyncAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IAsyncAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            return core.TryGetAsyncAlternateLookup(out lookup);
        }
#endif

        private CachePolicy CreatePolicy()
        {
            var calculator = core.policy.ExpiryCalculator;
            if (calculator != null)
            {
                var afterWrite = Optional<ITimePolicy>.None();
                var afterAccess = Optional<ITimePolicy>.None();
                var afterCustom = Optional<IDiscreteTimePolicy>.None();

                switch (calculator)
                {
                    case ExpireAfterAccess<K, V>:
                        afterAccess = new Optional<ITimePolicy>(this);
                        break;
                    case ExpireAfterWrite<K, V>:
                        afterWrite = new Optional<ITimePolicy>(this);
                        break;
                    default:
                        afterCustom = new Optional<IDiscreteTimePolicy>(this);
                        break;
                }

                return new CachePolicy(new Optional<IBoundedPolicy>(this), afterWrite, afterAccess, afterCustom);
            }

            return core.Policy;
        }

        TimeSpan ITimePolicy.TimeToLive => core.policy.ExpiryCalculator switch
        {
            ExpireAfterAccess<K, V> afterAccess => afterAccess.TimeToExpire,
            ExpireAfterWrite<K, V> afterWrite => afterWrite.TimeToExpire,
            _ => TimeSpan.Zero,
        };

        void ITimePolicy.TrimExpired() => DoMaintenance();
        void IDiscreteTimePolicy.TrimExpired() => DoMaintenance();

        public bool TryGetTimeToExpire<K1>(K1 key, out TimeSpan timeToExpire)
        {
            if (core.policy.ExpiryCalculator != null && key is K actualKey && core.TryGetNode(actualKey, out N? node) && node is TimeOrderNode<K, V> timeNode)
            {
                timeToExpire = (new Duration(timeNode.GetTimestamp()) - Duration.SinceEpoch()).ToTimeSpan();
                return true;
            }

            timeToExpire = default;
            return false;
        }

        private sealed class Proxy : ICacheEvents<K, V>
        {
            private readonly WeightedConcurrentLfu<K, V, N, P> lfu;

            internal Proxy(WeightedConcurrentLfu<K, V, N, P> lfu)
            {
                this.lfu = lfu;
            }

            public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                add
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemRemoved += value;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                remove
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemRemoved -= value;
                }
            }

#if NETCOREAPP3_0_OR_GREATER
            public event EventHandler<ItemUpdatedEventArgs<K, V>> ItemUpdated
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                add
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemUpdated += value;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                remove
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemUpdated -= value;
                }
            }
#endif
        }
    }
}
