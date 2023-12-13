using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu
{
    // LFU with time-based expiry policy.
    internal class ConcurrentTLfu<K, V> : ICache<K, V>, IAsyncCache<K, V>, IBoundedPolicy
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

        internal ConcurrentLfuCore<K, V, TimeOrderNode<K, V>, ExpireAfterPolicy<K, V>> Core => core;

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
        public CachePolicy Policy => core.Policy;

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

#if DEBUG
        /// <summary>
        /// Format the LFU as a string by converting all the keys to strings.
        /// </summary>
        /// <returns>The LFU formatted as a string.</returns>
        public string FormatLfuString()
        {
            return core.FormatLfuString();
        }
#endif

    }
}
