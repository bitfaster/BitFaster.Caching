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
    public class ScopedExtensionsTests
    {
        private ConcurrentLru<int, Scoped<Disposable>> lru = new(4);

        [Fact]
        public void GetOrAddRawRetrunsValidLifetime()
        {
            using (var l = lru.ScopedGetOrAdd(1, x => new Scoped<Disposable>(new Disposable())))
            {
                var d = l.Value.IsDisposed.Should().BeFalse();
            }
        }

        [Fact]
        public void GetOrAddWrappedRetrunsValidLifetime()
        {
            using (var l = lru.ScopedGetOrAdd(1, x => new Disposable()))
            {
                var d = l.Value.IsDisposed.Should().BeFalse();
            }
        }

        [Fact]
        public void GetOrAddWrappedProtectedRetrunsValidLifetime()
        {
            using (var l = lru.ScopedGetOrAddProtected(1, x => new Scoped<Disposable>(new Disposable())))
            {
                var d = l.Value.IsDisposed.Should().BeFalse();
            }
        }

        [Fact]
        public void GetOrAddWrappedProtectedRejectsDisposedObject()
        {
            var sd = new Scoped<Disposable>(new Disposable());
            sd.Dispose();

            lru.Invoking(l => l.ScopedGetOrAddProtected(1, x => sd)).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public async Task ScopedGetOrAddAsyncRetrunsValidLifetime()
        {
            using (var l = await lru.ScopedGetOrAddAsync(1, x => Task.FromResult(new Scoped<Disposable>(new Disposable()))))
            {
                var d = l.Value.IsDisposed.Should().BeFalse();
            }
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
