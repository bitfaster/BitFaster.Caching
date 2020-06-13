using FluentAssertions;
using Lightweight.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Lightweight.Caching.UnitTests
{
    public class ScopedTests
    {
        [Fact]
        public void WhenScopeIsCreatedThenScopeDisposedLifetimeDisposesValue()
        {
            var disposable = new Disposable();
            var scope = new Scoped<Disposable>(disposable);
            var lifetime = scope.CreateLifetime();

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
            var scope = new Scoped<Disposable>(disposable);
            var lifetime = scope.CreateLifetime();

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
            var scope = new Scoped<Disposable>(disposable);
            scope.Dispose();

            scope.Invoking(s => s.CreateLifetime()).Should().Throw<ObjectDisposedException>();
        }

        [Fact]
        public void WhenScopeIsCreatedFromCacheLifetimeCanBeCreatedAndDisposed()
        {
            var lru = new ConcurrentLru<int, Scoped<Disposable>>(2, 9, EqualityComparer<int>.Default);
            var valueFactory = new DisposableValueFactory();

            using (var lifetime = lru.GetOrAdd(1, valueFactory.Create).CreateLifetime())
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

            public Scoped<Disposable> Create(int key)
            {
                return new Scoped<Disposable>(this.Disposable);
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
