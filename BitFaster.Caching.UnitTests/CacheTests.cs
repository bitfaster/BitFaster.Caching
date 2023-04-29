
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    // Tests for interface default implementations.
    public class CacheTests
    {
// backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenCacheInterfaceDefaultGetOrAddFallback()
        {
            var cache = new Mock<ICache<int, int>>();
            cache.CallBase = true;

            Func<int, Func<int, int>, int> evaluate = (k, f) => f(k);
            cache.Setup(c => c.GetOrAdd(It.IsAny<int>(), It.IsAny<Func<int, int>>())).Returns(evaluate);

            cache.Object.GetOrAdd(
                1, 
                (k, a) => k + a, 
                2).Should().Be(3);
        }

        [Fact]
        public async Task WhenAsyncCacheInterfaceDefaultGetOrAddFallback()
        {
            var cache = new Mock<IAsyncCache<int, int>>();
            cache.CallBase = true;

            Func<int, Func<int, Task<int>>, ValueTask<int>> evaluate = (k, f) => new ValueTask<int>(f(k));
            cache.Setup(c => c.GetOrAddAsync(It.IsAny<int>(), It.IsAny<Func<int, Task<int>>>())).Returns(evaluate);

             var r = await cache.Object.GetOrAddAsync(
                1,
                (k, a) => Task.FromResult(k + a),
                2);
            
            r.Should().Be(3);
        }

        [Fact]
        public void WhenScopedCacheInterfaceDefaultGetOrAddFallback()
        {
            var cache = new Mock<IScopedCache<int, Disposable>>();
            cache.CallBase = true;

            Func<int, Func<int, Scoped<Disposable>>, Lifetime<Disposable>> evaluate = (k, f) =>
                {
                    var scope = f(k);
                    scope.TryCreateLifetime(out var lifetime).Should().BeTrue();
                    return lifetime;
                };

            cache.Setup(c => c.ScopedGetOrAdd(It.IsAny<int>(), It.IsAny<Func<int, Scoped<Disposable>>>())).Returns(evaluate);

            var l = cache.Object.ScopedGetOrAdd(
                1,
                (k, a) => new Scoped<Disposable>(new Disposable(k + a)),
                2);

            l.Value.State.Should().Be(3);
        }

        [Fact]
        public async Task WhenScopedAsyncCacheInterfaceDefaultGetOrAddFallback()
        {
            var cache = new Mock<IScopedAsyncCache<int, Disposable>>();
            cache.CallBase = true;

            Func<int, Func<int, Task<Scoped<Disposable>>>, ValueTask<Lifetime<Disposable>>> evaluate = async (k, f) =>
            {
                var scope = await f(k);
                scope.TryCreateLifetime(out var lifetime).Should().BeTrue();
                return lifetime;
            };

            cache
                .Setup(c => c.ScopedGetOrAddAsync(It.IsAny<int>(), It.IsAny<Func<int, Task<Scoped<Disposable>>>>()))
                .Returns(evaluate);

            var lifetime = await cache.Object.ScopedGetOrAddAsync(
               1,
               (k, a) => Task.FromResult(new Scoped<Disposable>(new Disposable(k + a))),
               2);

            lifetime.Value.State.Should().Be(3);
        }
#endif
    }
}
