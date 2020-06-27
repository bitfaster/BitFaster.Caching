using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        /// specified initial capacity, and uses the specified IEqualityComparer<T>.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the SingletonCache concurrently.</param>
        /// <param name="capacity">The initial number of elements that the SingletonCache can contain.</param>
        /// <param name="comparer">The IEqualityComparer<T> implementation to use when comparing keys.</param>
        public SingletonCache(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer)
        {
            this.cache = new ConcurrentDictionary<TKey, ReferenceCount<TValue>>(concurrencyLevel, capacity, comparer);
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

            return new Lifetime<TValue>(refCount.Value, () => this.Release(key));
        }

        private void Release(TKey key)
        {
            while (true)
            {
                var oldRefCount = this.cache[key];
                var newRefCount = oldRefCount.DecrementCopy();
                if (this.cache.TryUpdate(key, newRefCount, oldRefCount))
                {
                    if (newRefCount.Count == 0)
                    {
                        if (((IDictionary<TKey, ReferenceCount<TValue>>)this.cache).Remove(new KeyValuePair<TKey, ReferenceCount<TValue>>(key, newRefCount)))
                        {
                            if (newRefCount.Value is IDisposable d)
                            {
                                d.Dispose();
                            }
                        }
                    }
                    break;
                }
            }
        }
    }
}
