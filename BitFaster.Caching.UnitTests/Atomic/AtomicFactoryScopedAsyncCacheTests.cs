using System;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Atomic;
using Shouldly;
using Xunit;
using Moq;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AtomicFactoryScopedAsyncCacheTests : ScopedAsyncCacheTestBase
    {
        public AtomicFactoryScopedAsyncCacheTests()
            : base(new AtomicFactoryScopedAsyncCache<int, Disposable>(new ConcurrentLru<int, ScopedAsyncAtomicFactory<int, Disposable>>(capacity)))
        {
        }

        [Fact]
        public void WhenInnerCacheIsNullCtorThrows()
        {
            Action constructor = () => { var x = new AtomicFactoryScopedAsyncCache<int, Disposable>(null); };

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

        [Fact]
        public void WhenNoInnerEventsNoOuterEvents()
        {
            var inner = new Mock<ICache<int, ScopedAsyncAtomicFactory<int, Disposable>>>();
            inner.SetupGet(c => c.Events).Returns(Optional<ICacheEvents<int, ScopedAsyncAtomicFactory<int, Disposable>>>.None());

            var cache = new AtomicFactoryScopedAsyncCache<int, Disposable>(inner.Object);

            cache.Events.HasValue.ShouldBeFalse();
        }

        // Infer identified AddOrUpdate and TryUpdate as resource leaks. This test verifies correct disposal.
        [Fact]
        public void WhenEntryIsUpdatedOldEntryIsDisposed()
        {
            var disposable1 = new Disposable();
            var disposable2 = new Disposable();

            this.cache.AddOrUpdate(1, disposable1);

            this.cache.TryUpdate(1, disposable2).ShouldBeTrue();
            disposable1.IsDisposed.ShouldBeTrue();

            this.cache.TryUpdate(1, new Disposable()).ShouldBeTrue();
            disposable2.IsDisposed.ShouldBeTrue();
        }
    }
}
