using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu
{
    // Weighted LFU with events. Provided as a generic wrapper around ConcurrentLfuCore to support both
    // weighted access-order and weighted time-order node policies while exposing the events API.
    internal sealed class WeightedConcurrentLfu<K, V, N, P> : ICacheExt<K, V>, IAsyncCacheExt<K, V>, IBoundedPolicy, ITimePolicy, IDiscreteTimePolicy
        where K : notnull
        where N : LfuNode<K, V>
        where P : struct, INodePolicy<K, V, N, EventPolicy<K, V>>
    {
        // Note: for performance reasons this is a mutable struct, it cannot be readonly.
        private ConcurrentLfuCore<K, V, N, P, EventPolicy<K, V>> core;

        public WeightedConcurrentLfu(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer, P nodePolicy)
        {
            EventPolicy<K, V> eventPolicy = default;
            eventPolicy.SetEventSource(this);
            this.core = new(concurrencyLevel, capacity, scheduler, comparer, () => this.DrainBuffers(), nodePolicy, eventPolicy);
        }

        // structs cannot declare self referencing lambda functions, therefore pass this in from the ctor
        private void DrainBuffers()
        {
            this.core.DrainBuffers();
        }

        internal ConcurrentLfuCore<K, V, N, P, EventPolicy<K, V>> Core => core;

        ///<inheritdoc/>
        public int Count => core.Count;

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => core.Metrics;

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, V>> Events => new(new Proxy(this));

        internal ref EventPolicy<K, V> EventPolicyRef => ref this.core.eventPolicy;

        ///<inheritdoc/>
        public CachePolicy Policy => CreatePolicy();

        ///<inheritdoc/>
        public ICollection<K> Keys => core.Keys;

#if NET9_0_OR_GREATER
        /// <inheritdoc/>
        public IEqualityComparer<K> Comparer => this.core.Comparer;
#endif

        ///<inheritdoc/>
        public int Capacity => core.Capacity;

        ///<inheritdoc/>
        public IScheduler Scheduler => core.Scheduler;

        public void DoMaintenance()
        {
            core.DoMaintenance();
        }

        ///<inheritdoc/>
        public void AddOrUpdate(K key, V value)
        {
            core.AddOrUpdate(key, value);
        }

        ///<inheritdoc/>
        public void Clear()
        {
            core.Clear();
        }

        ///<inheritdoc/>
        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            return core.GetOrAdd(key, valueFactory);
        }

        ///<inheritdoc/>
        public V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
#if NET9_0_OR_GREATER
            where TArg : allows ref struct
#endif
        {
            return core.GetOrAdd(key, valueFactory, factoryArgument);
        }

        ///<inheritdoc/>
        public ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            return core.GetOrAddAsync(key, valueFactory);
        }

        ///<inheritdoc/>
        public ValueTask<V> GetOrAddAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
        {
            return core.GetOrAddAsync(key, valueFactory, factoryArgument);
        }

        ///<inheritdoc/>
        public void Trim(int itemCount)
        {
            core.Trim(itemCount);
        }

        ///<inheritdoc/>
        public bool TryGet(K key, [MaybeNullWhen(false)] out V value)
        {
            return core.TryGet(key, out value);
        }

        ///<inheritdoc/>
        public bool TryRemove(K key)
        {
            return core.TryRemove(key);
        }

        /// <summary>
        /// Attempts to remove the specified key value pair.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>true if the item was removed successfully; otherwise, false.</returns>
        public bool TryRemove(KeyValuePair<K, V> item)
        {
            return core.TryRemove(item);
        }

        /// <summary>
        /// Attempts to remove and return the value that has the specified key.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">When this method returns, contains the object removed, or the default value of the value type if key does not exist.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        public bool TryRemove(K key, [MaybeNullWhen(false)] out V value)
        {
            return core.TryRemove(key, out value);
        }

        ///<inheritdoc/>
        public bool TryUpdate(K key, V value)
        {
            return core.TryUpdate(key, value);
        }

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return core.GetEnumerator();
        }

        ///<inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return core.GetEnumerator();
        }

#if NET9_0_OR_GREATER
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAlternateLookup<TAlternateKey, K, V> GetAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            return core.GetAlternateLookup<TAlternateKey>();
        }

        ///<inheritdoc/>
        public bool TryGetAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            return core.TryGetAlternateLookup(out lookup);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncAlternateLookup<TAlternateKey, K, V> GetAsyncAlternateLookup<TAlternateKey>()
            where TAlternateKey : notnull, allows ref struct
        {
            return core.GetAsyncAlternateLookup<TAlternateKey>();
        }

        ///<inheritdoc/>
        public bool TryGetAsyncAlternateLookup<TAlternateKey>([MaybeNullWhen(false)] out IAsyncAlternateLookup<TAlternateKey, K, V> lookup)
            where TAlternateKey : notnull, allows ref struct
        {
            return core.TryGetAsyncAlternateLookup(out lookup);
        }
#endif

        private CachePolicy CreatePolicy()
        {
            var calc = core.policy.ExpiryCalculator;

            if (calc != null)
            {
                var afterWrite = Optional<ITimePolicy>.None();
                var afterAccess = Optional<ITimePolicy>.None();
                var afterCustom = Optional<IDiscreteTimePolicy>.None();

                switch (calc)
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

        TimeSpan ITimePolicy.TimeToLive
        {
            get
            {
                return core.policy.ExpiryCalculator switch
                {
                    ExpireAfterAccess<K, V> aa => aa.TimeToExpire,
                    ExpireAfterWrite<K, V> aw => aw.TimeToExpire,
                    _ => TimeSpan.Zero,
                };
            }
        }

        void ITimePolicy.TrimExpired() => DoMaintenance();

        void IDiscreteTimePolicy.TrimExpired() => DoMaintenance();

        ///<inheritdoc/>
        public bool TryGetTimeToExpire<K1>(K1 key, out TimeSpan timeToExpire)
        {
            if (core.policy.ExpiryCalculator != null && key is K k && core.TryGetNode(k, out N? node) && node is TimeOrderNode<K, V> timeNode)
            {
                var tte = new Duration(timeNode.GetTimestamp()) - Duration.SinceEpoch();
                timeToExpire = tte.ToTimeSpan();
                return true;
            }

            timeToExpire = default;
            return false;
        }

        // To get JIT optimizations, policies must be structs.
        // If the structs are returned directly via properties, they will be copied. Since
        // eventPolicy is a mutable struct, copy is bad since changes are lost.
        // Hence it is returned by ref and mutated via Proxy.
        private class Proxy : ICacheEvents<K, V>
        {
            private readonly WeightedConcurrentLfu<K, V, N, P> lfu;

            public Proxy(WeightedConcurrentLfu<K, V, N, P> lfu)
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

            // backcompat: remove conditional compile
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
