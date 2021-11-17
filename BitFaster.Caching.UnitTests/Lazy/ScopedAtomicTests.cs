using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lazy
{
    public class ScopedAtomicTests
    {
        [Fact]
        public void WhenScopeIsCreatedThenScopeDisposedLifetimeDisposesValue()
        {
            var disposable = new Disposable();
            var scope = new ScopedAtomic<int, Disposable>(disposable);
            scope.TryCreateLifetime(out var lifetime).Should().BeTrue();

            scope.Dispose();
            scope.Dispose(); // validate double dispose is still single ref count
            disposable.IsDisposed.Should().BeFalse();

            lifetime.Dispose();
            disposable.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenScopeIsCreatedThenLifetimeDisposedScopeDisposesValue()
        {
            var disposable = new Disposable();
            var scope = new ScopedAtomic<int, Disposable>(disposable);
            scope.TryCreateLifetime(out var lifetime).Should().BeTrue();

            lifetime.Dispose();
            lifetime.Dispose(); // validate double dispose is still single ref count

            disposable.IsDisposed.Should().BeFalse();

            scope.Dispose();
            disposable.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenScopeIsDisposedCreateScopeThrows()
        {
            var disposable = new Disposable();
            var scope = new ScopedAtomic<int, Disposable>(disposable);
            scope.Dispose();

            scope.Invoking(s => s.CreateLifetime(1, k => new Disposable())).Should().Throw<ObjectDisposedException>();
        }

        [Fact]
        public void WhenScopeIsNotDisposedCreateScopeReturnsLifetime()
        {
            var disposable = new Disposable();
            var scope = new ScopedAtomic<int, Disposable>(disposable);

            using (var l = scope.CreateLifetime(1, k => new Disposable()))
            {
                l.ReferenceCount.Should().Be(1);
            }
        }

        [Fact]
        public void WhenScopeIsDisposedTryCreateScopeReturnsFalse()
        {
            var disposable = new Disposable();
            var scope = new ScopedAtomic<int, Disposable>(disposable);
            scope.Dispose();

            scope.TryCreateLifetime(out var l).Should().BeFalse();
        }


        [Fact]
        public void WhenAtomicIsNotCreatedTryCreateScopeReturnsFalse()
        {
            var scope = new ScopedAtomic<int, Disposable>();

            scope.TryCreateLifetime(out var l).Should().BeFalse();
        }

        [Fact]
        public void WhenScopedIsCreatedFromCacheItemHasExpectedLifetime()
        {
            var lru = new ConcurrentLru<int, ScopedAtomic<int, Disposable>>(2, 9, EqualityComparer<int>.Default);
            var valueFactory = new DisposableValueFactory();

            using (var lifetime = lru.GetOrAdd(1, valueFactory.Create))
            {
                lifetime.Value.IsDisposed.Should().BeFalse();
            }

            valueFactory.Disposable.IsDisposed.Should().BeFalse();

            lru.TryRemove(1);

            valueFactory.Disposable.IsDisposed.Should().BeTrue();
        }

        private class DisposableValueFactory
        {
            public Disposable Disposable { get; } = new Disposable();

            public Disposable Create(int key)
            {
                return this.Disposable;
            }
        }

        private class Disposable : IDisposable
        {
            public bool IsDisposed { get; set; }

            public void Dispose()
            {
                this.IsDisposed.Should().BeFalse();
                IsDisposed = true;
            }
        }
    }
}
