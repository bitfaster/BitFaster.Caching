using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A cache decorator for working with  <see cref="AtomicFactory{K, V}"/> wrapped values, giving exactly once initialization.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    [DebuggerTypeProxy(typeof(CacheDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    public sealed class AtomicFactoryCache<K, V> : ICache<K, V>
    {
        private readonly ICache<K, AtomicFactory<K, V>> cache;
        private readonly Optional<ICacheEvents<K, V>> events;

        // single instance of what is being created - this becomes unbounded
        private readonly SingletonCache<K, AtomicFactory<K, V>> singleton = new SingletonCache<K, AtomicFactory<K, V>>();

        /// <summary>
        /// Initializes a new instance of the ScopedCache class with the specified inner cache.
        /// </summary>
        /// <param name="cache">The decorated cache.</param>
        public AtomicFactoryCache(ICache<K, AtomicFactory<K, V>> cache)
        {
            if (cache == null)
            {
                Ex.ThrowArgNull(ExceptionArgument.cache);
            }

            this.cache = cache;

            if (cache.Events.HasValue)
            {
                this.events = new Optional<ICacheEvents<K, V>>(new EventProxy(cache.Events.Value));
            }
            else
            {
                this.events = Optional<ICacheEvents<K, V>>.None();
            }
        }

        ///<inheritdoc/>
        public int Count => ExHandling.EnumerateCount(this.GetEnumerator());

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => this.cache.Metrics;

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, V>> Events => this.events;

        ///<inheritdoc/>
        public ICollection<K> Keys => this.cache.Keys;

        ///<inheritdoc/>
        public CachePolicy Policy => this.cache.Policy;

        ///<inheritdoc/>
        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new AtomicFactory<K, V>(value));
        }

        ///<inheritdoc/>
        public void Clear()
        {
            this.cache.Clear();
        }

        // options
        // 1. create atomically, only add to cache afterwards
        //      - increases dictionary operations from 2 to 6 for the success case.
        ///<inheritdoc/>
        //public V GetOrAdd(K key, Func<K, V> valueFactory)
        //{
        //    if (this.cache.TryGet(key, out var atomicFactory)) // 1
        //    {
        //        return atomicFactory.GetValue(key, valueFactory);
        //    }

        //    // this can be a race - you can exit the if statement after factory holder is disposed
        //    using (var factoryHolder = singleton.Acquire(key)) // 2
        //    {
        //        // double check to prevent race
        //        if (this.cache.TryGet(key, out var atomicFactory)) // 3
        //        {
        //            return atomicFactory.GetValue(key, valueFactory);
        //        }

        //        V value = factoryHolder.Value.GetValue(key, valueFactory);

        //        this.cache.GetOrAdd(key, _ => factoryHolder.Value); // 4

        //        return value;
        //    } // 5
        //}

        // 2. eager create wrapper, ignore wrapper on exception
        //      - potential race if there is interleaved fail then success, will evict items to add then remove the exception item

        // 3. eager create wrapper, ignore wrapper if value not created
        //      - need a way to filter out of public interface

        ///<inheritdoc/>
        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            var atomicFactory = cache.GetOrAdd(key, _ => new AtomicFactory<K, V>());
            return atomicFactory.GetValue(key, valueFactory);
        }

        ///<inheritdoc/>
        public bool TryGet(K key, out V value)
        {
            AtomicFactory<K, V> output;
            var ret = cache.TryGet(key, out output);

            if (ret && output.IsValueCreated)
            {
                value = output.ValueIfCreated;
                return true;
            }

            value = default;
            return false;
        }

        ///<inheritdoc/>
        public bool TryRemove(K key)
        {
            return cache.TryRemove(key);
        }

        ///<inheritdoc/>
        public bool TryUpdate(K key, V value)
        {
            return cache.TryUpdate(key, new AtomicFactory<K, V>(value));
        }

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var kvp in this.cache)
            {
                if (kvp.Value.IsValueCreated)
                {
                    yield return new KeyValuePair<K, V>(kvp.Key, kvp.Value.ValueIfCreated);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((AtomicFactoryCache<K, V>)this).GetEnumerator();
        }

        private class EventProxy : CacheEventProxyBase<K, AtomicFactory<K, V>, V>
        {
            public EventProxy(ICacheEvents<K, AtomicFactory<K, V>> inner)
                : base(inner)
            {
            }

            protected override ItemRemovedEventArgs<K, V> TranslateOnRemoved(ItemRemovedEventArgs<K, AtomicFactory<K, V>> inner)
            {
                return new ItemRemovedEventArgs<K, V>(inner.Key, inner.Value.ValueIfCreated, inner.Reason);
            }

            protected override ItemUpdatedEventArgs<K, V> TranslateOnUpdated(ItemUpdatedEventArgs<K, AtomicFactory<K, V>> inner)
            {
                return new ItemUpdatedEventArgs<K, V>(inner.Key, inner.OldValue.ValueIfCreated, inner.NewValue.ValueIfCreated);
            }
        }
    }
}
