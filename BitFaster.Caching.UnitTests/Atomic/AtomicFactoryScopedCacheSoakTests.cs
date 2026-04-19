using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Moq;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    [Collection("Soak")]
    public class AtomicFactoryScopedCacheSoakTests
    {
        private const int capacity = 6;
        private const int threadCount = 4;
        private const int soakIterations = 10;
        private const int loopIterations = 100_000;
#if NET9_0_OR_GREATER
        [Theory]
        [Repeat(soakIterations)]
        public async Task ScopedGetOrAddAlternateLifetimeIsAlwaysAlive(int _)
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();

            var run = Threaded.Run(threadCount, _ =>
            {
                var key = new char[8];

                for (int i = 0; i < loopIterations; i++)
                {
                    (i + 1).TryFormat(key, out int written);

                    using (var lifetime = alternate.ScopedGetOrAdd(key.AsSpan().Slice(0, written), k => { return new Scoped<Disposable>(new Disposable(int.Parse(k))); }))
                    {
                        lifetime.Value.IsDisposed.Should().BeFalse($"ref count {lifetime.ReferenceCount}");
                    }
                }
            });

            await run;
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task ScopedGetOrAddAlternateArgLifetimeIsAlwaysAlive(int _)
        {
            var cache = new AtomicFactoryScopedCache<string, Disposable>(new ConcurrentLru<string, ScopedAtomicFactory<string, Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();

            var run = Threaded.Run(threadCount, _ =>
            {
                var key = new char[8];

                for (int i = 0; i < loopIterations; i++)
                {
                    (i + 1).TryFormat(key, out int written);

                    using (var lifetime = alternate.ScopedGetOrAdd(key.AsSpan().Slice(0, written), (k, offset) => { return new Scoped<Disposable>(new Disposable(int.Parse(k) + offset)); }, 1))
                    {
                        lifetime.Value.IsDisposed.Should().BeFalse($"ref count {lifetime.ReferenceCount}");
                    }
                }
            });

            await run;
        }
#endif
    }
}
