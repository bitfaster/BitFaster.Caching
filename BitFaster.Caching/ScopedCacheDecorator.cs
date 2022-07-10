using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public interface IScopedCache<K, V> where V : IDisposable
    {
        int Count { get; }

        bool TryGet(K key, out Lifetime<V> value);

        Lifetime<V> GetOrAdd(K key, Func<K, V> valueFactory);

        Task<Lifetime<V>> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory);

        bool TryRemove(K key);

        bool TryUpdate(K key, V value);

        void AddOrUpdate(K key, V value);

        void Clear();

        void Trim(int itemCount);
    }

    // completely encapsulates all scope objects
    public class ScopedCacheDecorator<K, V> : IScopedCache<K, V> where V : IDisposable
    {
        private readonly ICache<K, Scoped<V>> cache;

        public ScopedCacheDecorator(ICache<K, Scoped<V>> cache)
        {
            this.cache = cache;
        }

        public int Count => cache.Count;

        public void AddOrUpdate(K key, V value)
        {
            this.cache.AddOrUpdate(key, new Scoped<V>(value));
        }

        public void Clear()
        {
            this.cache.Clear();
        }

        public Lifetime<V> GetOrAdd(K key, Func<K, V> valueFactory)
        {
            while (true)
            {
                // Note: allocates a closure on every call
                // alternative is Func<K, Task<Scoped<T>>> valueFactory input arg, but this lets the caller see the scoped object
                var scope = cache.GetOrAdd(key, k => new Scoped<V>(valueFactory(k)));

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }

        public async Task<Lifetime<V>> GetOrAddAsync(K key, Func<K, Task<V>> valueFactory)
        {
            while (true)
            {
                // Note: allocates a closure on every call
                var scope = await cache.GetOrAddAsync(key, async k =>
                {
                    var v = await valueFactory(k);
                    return new Scoped<V>(v);
                }).ConfigureAwait(false);

                if (scope.TryCreateLifetime(out var lifetime))
                {
                    return lifetime;
                }
            }
        }

        public void Trim(int itemCount)
        {
            this.cache.Trim(itemCount);
        }

        public bool TryGet(K key, out Lifetime<V> value)
        {
            if (this.cache.TryGet(key, out var scope))
            {
                if (scope.TryCreateLifetime(out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }

        public bool TryRemove(K key)
        {
            return this.cache.TryRemove(key);
        }

        public bool TryUpdate(K key, V value)
        {
            return this.cache.TryUpdate(key, new Scoped<V>(value));
        }
    }
}
