using System;
using System.Collections.Generic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class ScopedTests
    {
#if NETCOREAPP3_1_OR_GREATER
        private const long MaxExpectedBytesPerLifetime = 80L;
#endif

        [Fact]
        public void WhenScopeIsCreatedThenScopeDisposedValueIsDisposed()
        {
            var disposable = new Disposable();
            var scope = new Scoped<Disposable>(disposable);

            scope.Dispose();
            disposable.IsDisposed.Should().BeTrue();
        }

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
        public void WhenScopeIsDisposedTryCreateScopeReturnsFalse()
        {
            var disposable = new Disposable();
            var scope = new Scoped<Disposable>(disposable);
            scope.Dispose();

            scope.TryCreateLifetime(out var l).Should().BeFalse();
        }

        [Fact]
        public void WhenScopedIsCreatedFromCacheItemHasExpectedLifetime()
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

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void WhenLifetimeIsCreatedInternalReferenceCountingDoesNotAllocateOnHeap()
        {
            var scope = new Scoped<Disposable>(new Disposable());

            using (scope.CreateLifetime())
            {
            }

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 256; i++)
            {
                using var lifetime = scope.CreateLifetime();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            allocated.Should().BeLessThan(256 * MaxExpectedBytesPerLifetime);
        }
#endif
    }
}
