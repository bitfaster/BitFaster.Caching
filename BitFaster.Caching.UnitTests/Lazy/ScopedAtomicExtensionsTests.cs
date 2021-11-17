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
    public class ScopedAtomicExtensionsTests
    {
        private ConcurrentLru<int, ScopedAtomic<int, Disposable>> lru = new(2, 9, EqualityComparer<int>.Default);

        [Fact]
        public void GetOrAddRetrunsValidLifetime()
        {
            var valueFactory = new DisposableValueFactory();

            using (var lifetime = lru.GetOrAdd(1, valueFactory.Create))
            {
                lifetime.Value.IsDisposed.Should().BeFalse();
            }
        }

        [Fact]
        public void AddOrUpdateUpdatesValue()
        {
            var d = new Disposable();

            lru.AddOrUpdate(1, d);

            lru.TryGetLifetime(1, out var lifetime).Should().BeTrue();
            using (lifetime)
            {
                lifetime.Value.Should().Be(d);
            }
        }

        [Fact]
        public void TryUpdateWhenKeyDoesNotExistReturnsFalse()
        {
            var d = new Disposable();

            lru.TryUpdate(1, d).Should().BeFalse();
        }

        [Fact]
        public void TryUpdateWhenKeyExistsUpdatesValue()
        {
            var d1 = new Disposable();
            lru.AddOrUpdate(1, d1);

            var d2 = new Disposable();

            lru.TryUpdate(1, d2).Should().BeTrue();

            lru.TryGetLifetime(1, out var lifetime).Should().BeTrue();
            using (lifetime)
            {
                lifetime.Value.Should().Be(d2);
            }
        }

        [Fact]
        public void TryGetLifetimeDuringRaceReturnsFalse()
        {
            // directly add an uninitialized ScopedAtomic, simulating catching GetOrAdd before value is created
            lru.AddOrUpdate(1, new ScopedAtomic<int, Disposable>());

            lru.TryGetLifetime(1, out var lifetime).Should().BeFalse();
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
