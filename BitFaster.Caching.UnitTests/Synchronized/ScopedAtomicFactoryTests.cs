using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Synchronized
{
    public class ScopedAtomicFactoryTests
    {
        [Fact]
        public void WhenInitializedWithValueTryCreateLifetimeCreatesLifetimeWithValue()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>(expectedDisposable);

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(new Disposable()), out var lifetime).Should().BeTrue();

            lifetime.Value.Should().Be(expectedDisposable);
        }

        [Fact]
        public void WhenInitializedWithFactoryTryCreateLifetimeCreatesLifetimeWithValue()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(expectedDisposable), out var lifetime).Should().BeTrue();

            lifetime.Value.Should().Be(expectedDisposable);
        }

        [Fact]
        public void WhenInitializedWithFactoryValueIsCached()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(expectedDisposable), out var lifetime1).Should().BeTrue();
            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(new Disposable()), out var lifetime2).Should().BeTrue();

            lifetime2.Value.Should().Be(expectedDisposable);
        }

        [Fact]
        public void WhenInitializedWithValueThenDisposedCreateLifetimeIsFalse()
        {
            var sa = new ScopedAtomicFactory<int, Disposable>(new Disposable());
            sa.Dispose();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(new Disposable()), out var l).Should().BeFalse();
        }

        [Fact]
        public void WhenCreatedThenDisposedCreateLifetimeIsFalse()
        {
            var sa = new ScopedAtomicFactory<int, Disposable>();
            sa.Dispose();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(new Disposable()), out var l).Should().BeFalse();
        }

        [Fact]
        public void WhenInitializedLifetimeKeepsValueAlive()
        {
            var disposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(disposable), out var lifetime1).Should().BeTrue();
            sa.TryCreateLifetime(1, k => null, out var lifetime2).Should().BeTrue();

            sa.Dispose();
            disposable.IsDisposed.Should().BeFalse();

            lifetime1.Dispose();
            disposable.IsDisposed.Should().BeFalse();

            lifetime2.Dispose();
            disposable.IsDisposed.Should().BeTrue();
        }
    }
}
