﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BitFaster.Caching
{
    /// <summary>
    /// Cache a single value for each key, and maintain in memory only the values that have been acquired 
    /// but not yet released.
    /// </summary>
    /// <remarks>Based on LockObjectCache by Mayank Mehta.</remarks>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class SingletonCache<TKey, TValue>
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, ReferenceCount<TValue>> cache;

        /// <summary>
        /// Initializes a new instance of the SingletonCache class that is empty, has the default concurrency level, 
        /// has the default initial capacity, and uses the default comparer for the key type.
        /// </summary>
        public SingletonCache()
        {
            this.cache = new ConcurrentDictionary<TKey, ReferenceCount<TValue>>();
        }

        /// <summary>
        /// Initializes a new instance of the SingletonCache that has the specified concurrency level, has the 
        /// specified initial capacity, and uses the specified IEqualityComparer.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the SingletonCache concurrently.</param>
        /// <param name="capacity">The initial number of elements that the SingletonCache can contain.</param>
        /// <param name="comparer">The IEqualityComparer implementation to use when comparing keys.</param>
        public SingletonCache(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer)
        {
            this.cache = new ConcurrentDictionary<TKey, ReferenceCount<TValue>>(concurrencyLevel, ConcurrentDictionarySize.NextPrimeGreaterThan(capacity), comparer);
        }

        /// <summary>
        /// Acquire a singleton value for the specified key. The lifetime guarantees the value is alive and is a singleton 
        /// for the given key until the lifetime is disposed.
        /// </summary>
        /// <param name="key">The key of the item</param>
        /// <param name="valueFactory">The value factory</param>
        /// <returns>A value lifetime</returns>
        public Lifetime<TValue> Acquire(TKey key, Func<TKey, TValue> valueFactory)
        {
            var refCount = this.cache.AddOrUpdate(key,
                    (k) => new ReferenceCount<TValue>(valueFactory(k)),
                    (_, existingRefCount) => existingRefCount.IncrementCopy());

            return new Lifetime<TValue>(refCount, () => this.Release(key));
        }

        private void Release(TKey key)
        {
            while (true)
            {
                if (!this.cache.TryGetValue(key, out var oldRefCount))
                {
                    // already released, exit
                    break;
                }

                // if count is 1, the caller's decrement makes refcount 0: it is unreferenced and eligible to remove
                if (oldRefCount.Count == 1)
                {
                    var kvp = new KeyValuePair<TKey, ReferenceCount<TValue>>(key, oldRefCount);

                    // hidden atomic remove
                    // https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
                    if (((ICollection<KeyValuePair<TKey, ReferenceCount<TValue>>>)this.cache).Remove(kvp))
                    {
                        // no longer in cache, safe to dispose and exit
                        Disposer<TValue>.Dispose(oldRefCount.Value);
                        break;
                    }
                }
                else if (this.cache.TryUpdate(key, oldRefCount.DecrementCopy(), oldRefCount))
                {
                    // replaced with decremented copy, exit
                    break;
                }
            }
        }
    }
}
