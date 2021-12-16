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
    public class ScopedAsyncAtomicTests
    {
        private readonly ConcurrentLru<int, ScopedAsyncAtomic<int, SomeDisposable>> lru = new(4);

        [Fact]
        public async Task API()
        {
            var scopedAsyncAtomicLru = new ConcurrentLru<int, ScopedAsyncAtomic<int, SomeDisposable>>(4);
            Func<int, Task<SomeDisposable>> valueFactory = k => Task.FromResult(new SomeDisposable());

            using (var lifetime = await scopedAsyncAtomicLru.GetOrAddAsync(1, valueFactory))
            {
                var y = lifetime.Value;
            }

            scopedAsyncAtomicLru.TryUpdate(2, new SomeDisposable());

            scopedAsyncAtomicLru.AddOrUpdate(1, new SomeDisposable());

            // TODO: how to clean this up to 1 line?
            if (scopedAsyncAtomicLru.TryGetLifetime(1, out var lifetime2))
            {
                using (lifetime2)
                {
                    var x = lifetime2.Value;
                }
            }
        }

        [Fact]
        public void WhenScopeIsCreatedThenScopeDisposedLifetimeDisposesValue()
        {
            var disposable = new Disposable();
            var scope = new ScopedAsyncAtomic<int, Disposable>(disposable);

            scope.TryCreateLifetime(out var lifetime).Should().BeTrue();

            scope.Dispose();
            scope.Dispose(); // validate double dispose is still single ref count
            disposable.IsDisposed.Should().BeFalse();

            lifetime.Dispose();
            disposable.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public async Task WhenScopeIsCreatedAsyncThenScopeDisposedLifetimeDisposesValue()
        {
            var disposable = new Disposable();
            var scope = new ScopedAsyncAtomic<int, Disposable>(disposable);

            var r = await scope.TryCreateLifetimeAsync(1, k => Task.FromResult(disposable));

            r.succeeded.Should().BeTrue();

            scope.Dispose();
            scope.Dispose(); // validate double dispose is still single ref count
            disposable.IsDisposed.Should().BeFalse();

            r.lifetime.Dispose();
            disposable.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenScopeIsDisposedCreateScopeAsyncThrows()
        {
            var disposable = new Disposable();
            var scope = new ScopedAsyncAtomic<int, Disposable>(disposable);
            scope.Dispose();

            scope.Invoking(async s => await s.CreateLifetimeAsync(1, k => Task.FromResult(new Disposable()))).Should().Throw<ObjectDisposedException>();
        }

        [Fact]
        public void WhenScopeIsDisposedTryCreateScopeReturnsFalse()
        {
            var disposable = new Disposable();
            var scope = new ScopedAsyncAtomic<int, Disposable>(disposable);
            scope.Dispose();

            scope.TryCreateLifetime(out var lifetime).Should().BeFalse();
        }

        [Fact]
        public async Task WhenScopeIsDisposedTryCreateScopeAsyncReturnsFalse()
        {
            var disposable = new Disposable();
            var scope = new ScopedAsyncAtomic<int, Disposable>(disposable);
            scope.Dispose();

            var r = await scope.TryCreateLifetimeAsync(1, k => Task.FromResult(new Disposable()));
            r.succeeded.Should().Be(false);
        }

        // TODO: this doesn't work without guard on TryCreate.
        // where should value be initialized?
        // how does scoped atomic handled value factory throw?
        [Fact]
        public void WhenValueIsNotCreatedTryCreateScopeReturnsFalse()
        {
            var scope = new ScopedAsyncAtomic<int, Disposable>();

            scope.TryCreateLifetime(out var l).Should().BeFalse();
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
