using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu
{
    // LFU with time-based expiry policy. Provided as a wrapper around ConcurrentLfuCore to hide generic item and policy.
    internal sealed class ConcurrentTLfu<K, V> : ICacheExt<K, V>, IAsyncCacheExt<K, V>, IBoundedPolicy, ITimePolicy, IDiscreteTimePolicy
        where K : notnull
    {
        // Note: for performance reasons this is a mutable struct, it cannot be readonly.
        private ConcurrentLfuCore<K, V, TimeOrderNode<K, V>, ExpireAfterPolicy<K, V, EventPolicy<K, V>>, EventPolicy<K, V>> core;

        public ConcurrentTLfu(int capacity, IExpiryCalculator<K, V> expiryCalculator)
        {
            EventPolicy<K, V> eventPolicy = default;
            eventPolicy.SetEventSource(this);
            this.core = new(Defaults.ConcurrencyLevel, capacity, new ThreadPoolScheduler(), EqualityComparer<K>.Default, () => this.DrainBuffers(), new(expiryCalculator), eventPolicy);
        }

        public ConcurrentTLfu(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer, IExpiryCalculator<K, V> expiryCalculator)
        {
            EventPolicy<K, V> eventPolicy = default;
            eventPolicy.SetEventSource(this);
            this.core = new(concurrencyLevel, capacity, scheduler, comparer, () => this.DrainBuffers(), new(expiryCalculator), eventPolicy);
        }

        // structs cannot declare self referencing lambda functions, therefore pass this in from the ctor
        private void DrainBuffers()
        {
            this.core.DrainBuffers();
        }

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
            DoMaintenance();
        }

        ///<inheritdoc/>
        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            return core.GetOrAdd(key, valueFactory);
        }

        ///<inheritdoc/>
        public V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
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
            DoMaintenance();
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

        public bool TryRemove(KeyValuePair<K, V> item)
        {
            return core.TryRemove(item);
        }

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

        private CachePolicy CreatePolicy()
        {
            var afterWrite = Optional<ITimePolicy>.None();
            var afterAccess = Optional<ITimePolicy>.None();
            var afterCustom = Optional<IDiscreteTimePolicy>.None();

            var calc = core.policy.ExpiryCalculator;

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
            ;

            return new CachePolicy(new Optional<IBoundedPolicy>(this), afterWrite, afterAccess, afterCustom);
        }

        TimeSpan ITimePolicy.TimeToLive => (this.core.policy.ExpiryCalculator) switch
        {
            ExpireAfterAccess<K, V> aa => aa.TimeToExpire,
            ExpireAfterWrite<K, V> aw => aw.TimeToExpire,
            _ => TimeSpan.Zero,
        };

        ///<inheritdoc/>
        public bool TryGetTimeToExpire<K1>(K1 key, out TimeSpan timeToExpire)
        {
            if (key is K k && core.TryGetNode(k, out TimeOrderNode<K, V>? node))
            {
                var tte = new Duration(node.GetTimestamp()) - Duration.SinceEpoch();
                timeToExpire = tte.ToTimeSpan();
                return true;
            }

            timeToExpire = default;
            return false;
        }

        ///<inheritdoc/>
        public void TrimExpired()
        {
            DoMaintenance();
        }

        // To get JIT optimizations, policies must be structs.
        // If the structs are returned directly via properties, they will be copied. Since
        // eventPolicy is a mutable struct, copy is bad since changes are lost.
        // Hence it is returned by ref and mutated via Proxy.
        private class Proxy : ICacheEvents<K, V>
        {
            private readonly ConcurrentTLfu<K, V> lfu;

            public Proxy(ConcurrentTLfu<K, V> lfu)
            {
                this.lfu = lfu;
            }

            public event EventHandler<ItemRemovedEventArgs<K, V>> ItemRemoved
            {
                add
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemRemoved += value;
                }
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
                add
                {
                    ref var policy = ref this.lfu.EventPolicyRef;
                    policy.ItemUpdated += value;
                }
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
