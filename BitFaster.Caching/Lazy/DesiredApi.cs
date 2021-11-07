using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Lazy
{
    class DesiredApi
    {
        public static void HowToCacheALazy()
        {
            var lru = new ConcurrentLru<int, ScopedLazy<SomeDisposable>>(4);
            var factory = new ScopedLazyFactory();

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
        public static async Task HowToCacheAnAsyncLazy()
        {
            var lru = new ConcurrentLru<int, ScopedAsyncLazy<SomeDisposable>>(4);
            var factory = new ScopedAsyncLazyFactory();

            using (var lifetime = await lru.GetOrAdd(1, factory.Create).CreateLifetimeAsync())
            {
                // This is cleaned up by the magic GetAwaiter method
                SomeDisposable y = await lifetime.Value;
            }
        }
    }

    public class ScopedLazyFactory
    {
        public Task<SomeDisposable> CreateAsync(int key)
        {
            return Task.FromResult(new SomeDisposable());
        }

        public ScopedLazy<SomeDisposable> Create(int key)
        {
            return new ScopedLazy<SomeDisposable>(() => new SomeDisposable());
        }
    }

    public class ScopedAsyncLazyFactory
    {
        public Task<SomeDisposable> CreateAsync(int key)
        {
            return Task.FromResult(new SomeDisposable());
        }

        public ScopedAsyncLazy<SomeDisposable> Create(int key)
        {
            return new ScopedAsyncLazy<SomeDisposable>(() => Task.FromResult(new SomeDisposable()));
        }
    }

    public class SomeDisposable : IDisposable
    {
        public void Dispose()
        {

        }
    }
}
