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

        public Handle Acquire(TKey key, Func<TKey, TValue> valueFactory)
        {
            var refCount = this.cache.AddOrUpdate(key,
                    (_) => new ReferenceCount<TValue>(valueFactory(_)),
                    (_, existingRefCount) => existingRefCount.IncrementCopy());

            return new Handle(key, refCount.Value, this);
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
                        // This will remove from dictionary only if key and the value with ReferenceCount (== 0) matches (under a lock)
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

        public sealed class Handle : IDisposable
        {
            private TKey key;
            private TValue value;
            private SingletonCache<TKey, TValue> cache;

            public Handle(TKey key, TValue value, SingletonCache<TKey, TValue> cache)
            {
                this.key = key;
                this.value = value;
                this.cache = cache;
            }

            public TValue Value
            {
                get
                {
                    return this.value;
                }
            }

            public void Dispose()
            {
                if (this.cache != null)
                {
                    this.cache.Release(this.key);
                    this.cache = null;
                }
            }
        }
    }
}
