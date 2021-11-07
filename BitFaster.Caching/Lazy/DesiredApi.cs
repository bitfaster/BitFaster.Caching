using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Lazy
{
    class DesiredApi
    {
        public static void HowToCacheWithAtomicValueFactory()
        { 
            var lru = new ConcurrentLru<int, Atomic<int>>(4);

            // raw, this is a bit of a mess
            Atomic<int> r = lru.GetOrAdd(1, i => new Atomic<int>(() => i));

            // extension cleanup can hide it
            int rr = lru.GetOrAdd(1, i => i);
        }

        public static void HowToCacheADisposableAtomicValueFactory()
        {
            var lru = new ConcurrentLru<int, ScopedAtomic<SomeDisposable>>(4);
            var factory = new ScopedAtomicFactory();

            using (var lifetime = lru.GetOrAdd(1, factory.Create).CreateLifetime())
            {
                // options:
                // lazy lifetime = dupe class, cleaner API
                // extension method to avoid lifetime.value.value
                // just call lifetime.value.value (ugly)
                SomeDisposable y = lifetime.Value;
            }
        }

        // Requirements:
        // 1. lifetime/value create is async end to end (if async delegate is used to create value)
        // 2. value is created lazily, guarantee single instance of object, single invocation of lazy
        // 3. lazy value is disposed by scope
        // 4. lifetime keeps scope alive
#if NETCOREAPP3_1_OR_GREATER
        public static async Task HowToCacheADisposableAsyncLazy()
        {
            var lru = new ConcurrentLru<int, ScopedAtomicAsync<SomeDisposable>>(4);
            var factory = new ScopedAtomicAsyncFactory();

            await using (var lifetime = await lru.GetOrAdd(1, factory.Create).CreateLifetimeAsync())
            {
                // This is cleaned up by the magic GetAwaiter method
                SomeDisposable y = await lifetime.Task;
            }
        }
#endif
    }

    public class ScopedAtomicFactory
    {
        public Task<SomeDisposable> CreateAsync(int key)
        {
            return Task.FromResult(new SomeDisposable());
        }

        public ScopedAtomic<SomeDisposable> Create(int key)
        {
            return new ScopedAtomic<SomeDisposable>(() => new SomeDisposable());
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    public class ScopedAtomicAsyncFactory
    {
        public Task<SomeDisposable> CreateAsync(int key)
        {
            return Task.FromResult(new SomeDisposable());
        }

        public ScopedAtomicAsync<SomeDisposable> Create(int key)
        {
            return new ScopedAtomicAsync<SomeDisposable>(() => Task.FromResult(new SomeDisposable()));
        }
    }
#endif

    public class SomeDisposable : IDisposable
    {
        public void Dispose()
        {

        }
    }

    public static class AtomicCacheExtensions
    { 
        public static V GetOrAdd<K, V>(this ICache<K, Atomic<V>> cache, K key, Func<K, V> valueFactory)
        { 
            return cache.GetOrAdd(key, k => new Atomic<V>(() => valueFactory(k))).Value;
        }

        public static async Task<V> GetOrAddAsync<K, V>(this ICache<K, Atomic<V>> cache, K key, Func<K, V> valueFactory)
        { 
            var atomic = await cache.GetOrAddAsync(key, k => Task.FromResult(new Atomic<V>(() => valueFactory(k)))).ConfigureAwait(false);
            return atomic.Value;
        }
    }
}
