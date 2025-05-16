﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A cache decorator for working with  <see cref="AsyncAtomicFactory{K, V}"/> wrapped values, giving exactly once initialization.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values in the cache.</typeparam>
    [DebuggerTypeProxy(typeof(AtomicFactoryAsyncCache<,>.AsyncCacheDebugView))]
    [DebuggerDisplay("Count = {Count}")]
    public sealed class AtomicFactoryAsyncCache<K, V> : IAsyncCache<K, V>
        where K : notnull
    {
        private readonly ICache<K, AsyncAtomicFactory<K, V>> cache;
        private readonly Optional<ICacheEvents<K, V>> events;

        /// <summary>
        /// Initializes a new instance of the AtomicFactoryAsyncCache class with the specified inner cache.
        /// </summary>
        /// <param name="cache">The decorated cache.</param>
        public AtomicFactoryAsyncCache(ICache<K, AsyncAtomicFactory<K, V>> cache)
        {
            if (cache == null)
                Throw.ArgNull(ExceptionArgument.cache);

            this.cache = cache;

            if (cache.Events.HasValue)
            {
                this.events = new Optional<ICacheEvents<K, V>>(new EventProxy(cache.Events.Value!));
            }
            else
            {
                this.events = Optional<ICacheEvents<K, V>>.None();
            }
        }

        ///<inheritdoc/>
        public int Count => AtomicEx.EnumerateCount(this.GetEnumerator());

        ///<inheritdoc/>
        public Optional<ICacheMetrics> Metrics => cache.Metrics;

        ///<inheritdoc/>
        public Optional<ICacheEvents<K, V>> Events => this.events;

        ///<inheritdoc/>
        public ICollection<K> Keys => AtomicEx.FilterKeys<K, AsyncAtomicFactory<K, V>>(this.cache, v => v.IsValueCreated);

        ///<inheritdoc/>
        public CachePolicy Policy => this.cache.Policy;

        ///<inheritdoc/>
        public void AddOrUpdate(K key, V value)
        {
            cache.AddOrUpdate(key, new AsyncAtomicFactory<K, V>(value));
        }

        ///<inheritdoc/>
        public void Clear()
        {
            cache.Clear();
        }

        ///<inheritdoc/>
        public ValueTask<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            var synchronized = cache.GetOrAdd(key, _ => new AsyncAtomicFactory<K, V>());
            return synchronized.GetValueAsync(key, valueFactory);
        }

        /// <summary>
        /// Adds a key/value pair to the cache if the key does not already exist. Returns the new value, or the 
        /// existing value if the key already exists.
        /// </summary>
        /// <typeparam name="TArg">The type of an argument to pass into valueFactory.</typeparam>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The factory function used to asynchronously generate a value for the key.</param>
        /// <param name="factoryArgument">An argument value to pass into valueFactory.</param>
        /// <returns>A task that represents the asynchronous GetOrAdd operation.</returns>
        public ValueTask<V> GetOrAddAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
        {
            var synchronized = cache.GetOrAdd(key, _ => new AsyncAtomicFactory<K, V>());
            return synchronized.GetValueAsync(key, valueFactory, factoryArgument);
        }

        ///<inheritdoc/>
        public bool TryGet(K key, [MaybeNullWhen(false)] out V value)
        {
            AsyncAtomicFactory<K, V>? output;
            var ret = cache.TryGet(key, out output);

            if (ret && output!.IsValueCreated)
            {
                value = output.ValueIfCreated!;
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

        // backcompat: remove conditional compile
#if NET
        ///<inheritdoc/>
        ///<remarks>
        ///If the value factory is still executing, returns false.
        ///</remarks>
        public bool TryRemove(KeyValuePair<K, V> item)
        {
            var kvp = new KeyValuePair<K, AsyncAtomicFactory<K, V>>(item.Key, new AsyncAtomicFactory<K, V>(item.Value));
            return cache.TryRemove(kvp);
        }

        ///<inheritdoc/>
        /// <remarks>
        /// If the value factory is still executing, the default value will be returned.
        /// </remarks>
        public bool TryRemove(K key, [MaybeNullWhen(false)] out V value)
        {
            if (cache.TryRemove(key, out var atomic))
            {
                value = atomic.ValueIfCreated!;
                return true;
            }

            value = default;
            return false;
        }
#endif

        ///<inheritdoc/>
        public bool TryUpdate(K key, V value)
        {
            return cache.TryUpdate(key, new AsyncAtomicFactory<K, V>(value));
        }

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var kvp in this.cache)
            {
                if (kvp.Value.IsValueCreated)
                { 
                    yield return new KeyValuePair<K, V>(kvp.Key, kvp.Value.ValueIfCreated!); 
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((AtomicFactoryAsyncCache<K, V>)this).GetEnumerator();
        }

        private class EventProxy : CacheEventProxyBase<K, AsyncAtomicFactory<K, V>, V>
        {
            public EventProxy(ICacheEvents<K, AsyncAtomicFactory<K, V>> inner)
                : base(inner)
            {
            }

            protected override ItemRemovedEventArgs<K, V> TranslateOnRemoved(ItemRemovedEventArgs<K, AsyncAtomicFactory<K, V>> inner)
            {
                return new ItemRemovedEventArgs<K, V>(inner.Key, inner.Value!.ValueIfCreated, inner.Reason);
            }

            protected override ItemUpdatedEventArgs<K, V> TranslateOnUpdated(ItemUpdatedEventArgs<K, AsyncAtomicFactory<K, V>> inner)
            {
                return new ItemUpdatedEventArgs<K, V>(inner.Key, inner.OldValue!.ValueIfCreated, inner.NewValue!.ValueIfCreated);
            }
        }

        [ExcludeFromCodeCoverage]
        internal class AsyncCacheDebugView
        {
            private readonly IAsyncCache<K, V> cache;

            public AsyncCacheDebugView(IAsyncCache<K, V> cache)
            {
                this.cache = cache;
            }

            public KeyValuePair<K, V>[] Items
            {
                get
                {
                    var items = new KeyValuePair<K, V>[cache.Count];

                    int index = 0;
                    foreach (var kvp in cache)
                    {
                        items[index++] = kvp;
                    }
                    return items;
                }
            }

            public ICacheMetrics? Metrics => cache.Metrics.Value;
        }
    }
}
