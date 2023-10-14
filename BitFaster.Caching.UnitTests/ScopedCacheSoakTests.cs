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
                await Threaded.Run(4, () => {
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
    }
}
