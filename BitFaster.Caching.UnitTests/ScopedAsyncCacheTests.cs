using System;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ScopedAsyncCacheTests : ScopedAsyncCacheTestBase
    {
        public ScopedAsyncCacheTests() 
            : base(new ScopedAsyncCache<int, Disposable>(new ConcurrentLru<int, Scoped<Disposable>>(capacity)))
        {
        }

        [Fact]
        public void WhenInnerCacheIsNullCtorThrows()
        {
            Action constructor = () => { var x = new ScopedAsyncCache<int, Disposable>(null); };

            constructor.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task WhenScopeIsDisposedTryGetReturnsFalse()
        {
            var scope = new Scoped<Disposable>(new Disposable());

            await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(scope));

            scope.Dispose();

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeFalse();
        }

        [Fact]
        public async Task WhenKeyDoesNotExistGetOrAddAsyncAddsValue()
        {
            await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(new Scoped<Disposable>(new Disposable())));

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();
        }

        [Fact]
        public async Task GetOrAddAsyncDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Func<Task> getOrAdd = async () => { await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(scope)); };

            await getOrAdd.Should().ThrowAsync<InvalidOperationException>();
        }

// backcompat: remove conditional compile
#if NET
        [Fact]
        public async Task GetOrAddAsyncArgDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Func<Task> getOrAdd = async () => { await this.cache.ScopedGetOrAddAsync(1, (k, a) => Task.FromResult(scope), 2); };

            await getOrAdd.Should().ThrowAsync<InvalidOperationException>();
        }
#endif
    }
}
