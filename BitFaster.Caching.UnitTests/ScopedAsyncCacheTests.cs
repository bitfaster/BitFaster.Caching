using System;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using Shouldly;
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

            constructor.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public async Task WhenScopeIsDisposedTryGetReturnsFalse()
        {
            var scope = new Scoped<Disposable>(new Disposable());

            await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(scope));

            scope.Dispose();

            this.cache.ScopedTryGet(1, out var lifetime).ShouldBeFalse();
        }

        [Fact]
        public async Task WhenKeyDoesNotExistGetOrAddAsyncAddsValue()
        {
            await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(new Scoped<Disposable>(new Disposable())));

            this.cache.ScopedTryGet(1, out var lifetime).ShouldBeTrue();
        }

        [Fact]
        public async Task GetOrAddAsyncDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Func<Task> getOrAdd = async () => { await this.cache.ScopedGetOrAddAsync(1, k => Task.FromResult(scope)); };

            var ex = await getOrAdd.ShouldThrowAsync<InvalidOperationException>();;
        }

// backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public async Task GetOrAddAsyncArgDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Func<Task> getOrAdd = async () => { await this.cache.ScopedGetOrAddAsync(1, (k, a) => Task.FromResult(scope), 2); };

            var ex = await getOrAdd.ShouldThrowAsync<InvalidOperationException>();;
        }
#endif
    }
}
