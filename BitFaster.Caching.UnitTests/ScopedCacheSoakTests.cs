using System;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    [Collection("Soak")]
    public class ScopedCacheSoakTests
    {
        protected const int capacity = 6;
        protected readonly IScopedCache<int, Disposable> cache = new ScopedCache<int, Disposable>(new ConcurrentLru<int, Scoped<Disposable>>(capacity));

        [Fact]
        public async Task WhenSoakScopedGetOrAddValueIsAlwaysAlive()
        {
            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, () =>
                {
                    for (int j = 0; j < 100000; j++)
                    {
                        using (var l = this.cache.ScopedGetOrAdd(j, k => new Scoped<Disposable>(new Disposable(k))))
                        {
                            l.Value.IsDisposed.Should().BeFalse($"ref count {l.ReferenceCount}");
                        }
                    }
                });
            }
        }

#if NET9_0_OR_GREATER
        [Fact]
        public async Task ScopedGetOrAdd_ConcurrentWithRemove_ReturnedLifetimeIsAlive()
        {
            var scopedCache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, 1, StringComparer.Ordinal));
            var alternateLookup = scopedCache.GetAlternateLookup<ReadOnlySpan<char>>();

            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, r =>
                {
                    for (int j = 0; j < 100000; j++)
                    {
                        ReadOnlySpan<char> key = "42";

                        if (r == 0 && (j & 1) == 0)
                        {
                            alternateLookup.TryRemove(key, out _);
                        }

                        using var lifetime = (r & 1) == 0
                            ? alternateLookup.ScopedGetOrAdd(key, static k => new Scoped<Disposable>(new Disposable(int.Parse(k))))
                            : alternateLookup.ScopedGetOrAdd(key, static (k, offset) => new Scoped<Disposable>(new Disposable(int.Parse(k) + offset)), 0);

                        lifetime.Value.IsDisposed.Should().BeFalse($"ref count {lifetime.ReferenceCount}");
                        lifetime.Value.State.Should().Be(42);
                    }
                });
            }
        }
#endif
    }
}
