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
        public async Task WhenSoakAlternateScopedGetOrAddValueIsAlwaysAlive()
        {
            const int keyBufferLength = 5;
            const int threadCount = 4;
            var scopedCache = new ScopedCache<string, Disposable>(new ConcurrentLru<string, Scoped<Disposable>>(1, capacity, StringComparer.Ordinal));
            var alternateLookup = scopedCache.GetAlternateLookup<ReadOnlySpan<char>>();

            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(threadCount, _ =>
                {
                    var key = new char[keyBufferLength];
                    for (int j = 0; j < 100000; j++)
                    {
                        j.TryFormat(key, out int written);
                        using var lifetime = alternateLookup.ScopedGetOrAdd(key.AsSpan(0, written), static k => new Scoped<Disposable>(new Disposable(int.Parse(k))));

                        lifetime.Value.IsDisposed.Should().BeFalse($"ref count {lifetime.ReferenceCount}");
                    }
                });
            }
        }
#endif
    }
}
