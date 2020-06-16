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
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class SingletonCache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, ReferenceCount<TValue>> cache;

        public SingletonCache()
        {
            this.cache = new ConcurrentDictionary<TKey, ReferenceCount<TValue>>();
        }

        public SingletonCache(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer)
        {
            this.cache = new ConcurrentDictionary<TKey, ReferenceCount<TValue>>(concurrencyLevel, capacity, comparer);
        }

        public Lifetime<TValue> Acquire(TKey key, Func<TKey, TValue> valueFactory)
        {
            var refCount = this.cache.AddOrUpdate(key,
                    (_) => new ReferenceCount<TValue>(valueFactory(_)),
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
