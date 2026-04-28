using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    [Collection("Soak")]
    public class AtomicFactoryScopedAsyncCacheSoakTests
    {
        private const int capacity = 6;
        private const int threadCount = 4;
        private const int soakIterations = 10;
        private const int loopIterations = 100_000;

#if NET9_0_OR_GREATER
        [Theory]
        [Repeat(soakIterations)]
        public async Task ScopedGetOrAddAsyncAlternateLifetimeIsAlwaysAlive(int _)
        {
            var cache = new AtomicFactoryScopedAsyncCache<string, Disposable>(new ConcurrentLru<string, ScopedAsyncAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();

            var run = Threaded.RunAsync(threadCount, async _ =>
            {
                var key = new char[8];

                for (int i = 0; i < loopIterations; i++)
                {
                    (i + 1).TryFormat(key, out int written);

                    using var lifetime = await alternate.ScopedGetOrAddAsync(key.AsSpan().Slice(0, written), static k => Task.FromResult(new Scoped<Disposable>(new Disposable(int.Parse(k)))));
                    lifetime.Value.IsDisposed.Should().BeFalse($"ref count {lifetime.ReferenceCount}");
                }
            });

            await run;
        }
#endif
    }
}
