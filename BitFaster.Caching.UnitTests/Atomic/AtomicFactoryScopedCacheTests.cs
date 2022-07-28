using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Atomic;
using FluentAssertions;
using Xunit;
using Moq;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AtomicFactoryScopedCacheTests : ScopedCacheTestBase
    {
        public AtomicFactoryScopedCacheTests()
            : base(new AtomicFactoryScopedCache<int, Disposable>(new ConcurrentLru<int, ScopedAtomicFactory<int, Disposable>>(capacity)))
        {
        }

        [Fact]
        public void WhenInnerCacheIsNullCtorThrows()
        {
            Action constructor = () => { var x = new AtomicFactoryScopedCache<int, Disposable>(null); };

            constructor.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenScopeIsDisposedTryGetReturnsFalse()
        {
            var scope = new Scoped<Disposable>(new Disposable());

            this.cache.ScopedGetOrAdd(1, k => scope);

            scope.Dispose();

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistGetOrAddAddsValue()
        {
            this.cache.ScopedGetOrAdd(1, k => new Scoped<Disposable>(new Disposable()));

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();
        }

        [Fact]
        public void GetOrAddDisposedScopeThrows()
        {
            var scope = new Scoped<Disposable>(new Disposable());
            scope.Dispose();

            Action getOrAdd = () => { this.cache.ScopedGetOrAdd(1, k => scope); };

            getOrAdd.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void WhenNoInnerEventsNoOuterEvents()
        {
            var inner = new Mock<ICache<int, ScopedAtomicFactory<int, Disposable>>>();
            inner.SetupGet(c => c.Events).Returns(Optional<ICacheEvents<int, ScopedAtomicFactory<int, Disposable>>>.None());

            var cache = new AtomicFactoryScopedCache<int, Disposable>(inner.Object);

            cache.Events.HasValue.Should().BeFalse();
        }
    }
}
