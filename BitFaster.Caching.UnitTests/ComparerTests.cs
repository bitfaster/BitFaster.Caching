#if NET9_0_OR_GREATER
using System;
using System.Collections.Generic;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ComparerTests
    {
        [Fact]
        public void CacheImplementationsExposeConfiguredComparer()
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var expiry = new ExpireAfterWrite<string, int>(TimeSpan.FromMinutes(1));

            var caches = new (string Name, ICache<string, int> Cache)[]
            {
                ("ConcurrentLru", new ConcurrentLru<string, int>(1, 3, comparer)),
                ("FastConcurrentLru", new FastConcurrentLru<string, int>(1, 3, comparer)),
                ("ClassicLru", new ClassicLru<string, int>(1, 3, comparer)),
                ("ConcurrentTLru", new ConcurrentTLru<string, int>(1, 3, comparer, TimeSpan.FromMinutes(1))),
                ("FastConcurrentTLru", new FastConcurrentTLru<string, int>(1, 3, comparer, TimeSpan.FromMinutes(1))),
                ("ConcurrentLfu", new ConcurrentLfu<string, int>(1, 3, new NullScheduler(), comparer)),
                ("ConcurrentTLfu", new ConcurrentTLfu<string, int>(1, 3, new NullScheduler(), comparer, expiry)),
                ("AtomicFactoryCache", new AtomicFactoryCache<string, int>(new ConcurrentLru<string, AtomicFactory<string, int>>(1, 3, comparer))),
            };

            foreach (var (name, cache) in caches)
            {
                cache.Comparer.Should().BeSameAs(comparer, name);
            }
        }

        [Fact]
        public void AsyncCacheImplementationsExposeConfiguredComparer()
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var expiry = new ExpireAfterWrite<string, int>(TimeSpan.FromMinutes(1));

            var caches = new (string Name, IAsyncCache<string, int> Cache)[]
            {
                ("ConcurrentLru", new ConcurrentLru<string, int>(1, 3, comparer)),
                ("FastConcurrentLru", new FastConcurrentLru<string, int>(1, 3, comparer)),
                ("ClassicLru", new ClassicLru<string, int>(1, 3, comparer)),
                ("ConcurrentTLru", new ConcurrentTLru<string, int>(1, 3, comparer, TimeSpan.FromMinutes(1))),
                ("FastConcurrentTLru", new FastConcurrentTLru<string, int>(1, 3, comparer, TimeSpan.FromMinutes(1))),
                ("ConcurrentLfu", new ConcurrentLfu<string, int>(1, 3, new NullScheduler(), comparer)),
                ("ConcurrentTLfu", new ConcurrentTLfu<string, int>(1, 3, new NullScheduler(), comparer, expiry)),
                ("AtomicFactoryAsyncCache", new AtomicFactoryAsyncCache<string, int>(new ConcurrentLru<string, AsyncAtomicFactory<string, int>>(1, 3, comparer))),
            };

            foreach (var (name, cache) in caches)
            {
                cache.Comparer.Should().BeSameAs(comparer, name);
            }
        }
    }
}
#endif
