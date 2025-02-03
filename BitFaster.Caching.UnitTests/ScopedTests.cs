using Shouldly;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ScopedTests
    {
        [Fact]
        public void WhenScopeIsCreatedThenScopeDisposedValueIsDisposed()
        {
            var disposable = new Disposable();
            var scope = new Scoped<Disposable>(disposable);

            scope.Dispose();
            disposable.IsDisposed.ShouldBeTrue();
        }

        [Fact]
        public void WhenScopeIsCreatedThenScopeDisposedLifetimeDisposesValue()
        {
            var disposable = new Disposable();
            var scope = new Scoped<Disposable>(disposable);
            var lifetime = scope.CreateLifetime();

            scope.Dispose();
            scope.Dispose(); // validate double dispose is still single ref count
            disposable.IsDisposed.ShouldBeFalse();

            lifetime.Dispose();
            disposable.IsDisposed.ShouldBeTrue();
        }

        [Fact]
        public void WhenScopeIsCreatedThenLifetimeDisposedScopeDisposesValue()
        {
            var disposable = new Disposable();
            var scope = new Scoped<Disposable>(disposable);
            var lifetime = scope.CreateLifetime();

            lifetime.Dispose();
            lifetime.Dispose(); // validate double dispose is still single ref count

            disposable.IsDisposed.ShouldBeFalse();

            scope.Dispose();
            disposable.IsDisposed.ShouldBeTrue();
        }

        [Fact]
        public void WhenScopeIsDisposedCreateScopeThrows()
        {
            var disposable = new Disposable();
            var scope = new Scoped<Disposable>(disposable);
            scope.Dispose();

            Should.Throw<ObjectDisposedException>(() => scope.CreateLifetime());
        }

        [Fact]
        public void WhenScopeIsDisposedTryCreateScopeReturnsFalse()
        {
            var disposable = new Disposable();
            var scope = new Scoped<Disposable>(disposable);
            scope.Dispose();

            scope.TryCreateLifetime(out var l).ShouldBeFalse();
        }

        [Fact]
        public void WhenScopedIsCreatedFromCacheItemHasExpectedLifetime()
        {
            var lru = new ConcurrentLru<int, Scoped<Disposable>>(2, 9, EqualityComparer<int>.Default);
            var valueFactory = new DisposableValueFactory();

            using (var lifetime = lru.GetOrAdd(1, valueFactory.Create).CreateLifetime())
            {
                lifetime.Value.IsDisposed.ShouldBeFalse();
            }

            valueFactory.Disposable.IsDisposed.ShouldBeFalse();

            lru.TryRemove(1);

            valueFactory.Disposable.IsDisposed.ShouldBeTrue();
        }
    }
}
