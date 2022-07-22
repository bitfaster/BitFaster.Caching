using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Synchronized
{
    public class AtomicFactoryScopedCache<K, V> : IScopedCache<K, V> where V : IDisposable
    {
        private readonly ICache<K, ScopedAtomicFactory<K, V>> cache;

        public AtomicFactoryScopedCache(ICache<K, ScopedAtomicFactory<K, V>> cache)
        {
            this.cache = cache;
        }

        public int Capacity => this.cache.Capacity;

        public int Count => this.cache.Count;

        public ICacheMetrics Metrics => this.cache.Metrics;

        public ICacheEvents<K, Scoped<V>> Events => throw new NotImplementedException();

        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new ScopedAtomicFactory<K, V>(value));
        }

        public void Clear()
        {
            this.cache.Clear();
        }

        // TODO: dedupe
        private const int MaxRetry = 5;
        private static readonly string RetryFailureMessage = $"Exceeded {MaxRetry} attempts to create a lifetime.";

        public Lifetime<V> ScopedGetOrAdd(K key, Func<K, Scoped<V>> valueFactory)
        {
            int c = 0;
            var spinwait = new SpinWait();
            while (true)
            {
                var scope = cache.GetOrAdd(key, _ => new ScopedAtomicFactory<K, V>());

                if (scope.TryCreateLifetime(key, valueFactory, out var lifetime))
                {
                    return lifetime;
                }

                spinwait.SpinOnce();

                if (c++ > MaxRetry)
                {
                    throw new InvalidOperationException(RetryFailureMessage);
                }
            }
        }

        public Task<Lifetime<V>> ScopedGetOrAddAsync(K key, Func<K, Task<Scoped<V>>> valueFactory)
        {
            throw new NotImplementedException();
        }

        public bool ScopedTryGet(K key, out Lifetime<V> lifetime)
        {
            throw new NotImplementedException();
        }

        public void Trim(int itemCount)
        {
            throw new NotImplementedException();
        }

        public bool TryRemove(K key)
        {
            throw new NotImplementedException();
        }

        public bool TryUpdate(K key, V value)
        {
            throw new NotImplementedException();
        }
    }
}
