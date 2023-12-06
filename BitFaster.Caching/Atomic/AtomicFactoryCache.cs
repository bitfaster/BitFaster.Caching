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
        where K : notnull
    {
        private readonly ICache<K, AtomicFactory<K, V>> cache;
        private readonly Optional<ICacheEvents<K, V>> events;

        /// <summary>
        /// Initializes a new instance of the ScopedCache class with the specified inner cache.
        /// </summary>
        /// <param name="cache">The decorated cache.</param>
        public AtomicFactoryCache(ICache<K, AtomicFactory<K, V>> cache)
        {
            if (cache == null)
                Throw.ArgNull(ExceptionArgument.cache);

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
        public int Count => AtomicEx.EnumerateCount(this.GetEnumerator());

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => this.cache.Metrics;

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, V>> Events => this.events;

        ///<inheritdoc/>
        public ICollection<K> Keys => AtomicEx.FilterKeys<K, AtomicFactory<K, V>>(this.cache, v => v.IsValueCreated);

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

        ///<inheritdoc/>
        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            var atomicFactory = cache.GetOrAdd(key, _ => new AtomicFactory<K, V>());
            return atomicFactory.GetValue(key, valueFactory);
        }

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to generate a value for the key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>The value for the key. This will be either the existing value for the key if the key is already 
        /// in the cache, or the new value if the key was not in the cache.</returns>
        public V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
        {
            var atomicFactory = cache.GetOrAdd(key, _ => new AtomicFactory<K, V>());
            return atomicFactory.GetValue(key, valueFactory, factoryArgument);
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

        // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        ///<inheritdoc/>
        ///<remarks>
        ///If the value factory is still executing, returns false.
        ///</remarks>
        public bool TryRemove(KeyValuePair<K, V> item)
        {
            var kvp = new KeyValuePair<K, AtomicFactory<K, V>>(item.Key, new AtomicFactory<K, V>(item.Value));
            return cache.TryRemove(kvp);
        }

        ///<inheritdoc/>
        /// <remarks>
        /// If the value factory is still executing, the default value will be returned.
        /// </remarks>
        public bool TryRemove(K key, out V value)
        {
            if (cache.TryRemove(key, out var atomic))
            {
                value = atomic.ValueIfCreated;
                return true;
            }

            value = default;
            return false;
        }
#endif

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
