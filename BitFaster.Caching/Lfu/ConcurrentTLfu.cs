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
    internal sealed class ConcurrentTLfu<K, V> : ICacheExt<K, V>, IAsyncCache<K, V>, IBoundedPolicy, ITimePolicy, IDiscreteTimePolicy
        where K : notnull
    {
        // Note: for performance reasons this is a mutable struct, it cannot be readonly.
        private ConcurrentLfuCore<K, V, TimeOrderNode<K, V>, ExpireAfterPolicy<K, V>> core;

        public ConcurrentTLfu(int capacity, IExpiryCalculator<K, V> expiryCalculator)
        {
            this.core = new(Defaults.ConcurrencyLevel, capacity, new ThreadPoolScheduler(), EqualityComparer<K>.Default, () => this.DrainBuffers(), new(expiryCalculator));
        }

        public ConcurrentTLfu(int concurrencyLevel, int capacity, IScheduler scheduler, IEqualityComparer<K> comparer, IExpiryCalculator<K, V> expiryCalculator)
        {
            this.core = new(concurrencyLevel, capacity, scheduler, comparer, () => this.DrainBuffers(), new(expiryCalculator));
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
        public Optional<ICacheEvents<K, V>> Events => Optional<ICacheEvents<K, V>>.None();

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
            };

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
    }
}
