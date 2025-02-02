using BitFaster.Caching.Atomic;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class ScopedAtomicFactoryTests
    {
        [Fact]
        public void WhenInitializedWithValueTryCreateLifetimeCreatesLifetimeWithValue()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>(expectedDisposable);

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(new Disposable()), out var lifetime).ShouldBeTrue();

            lifetime.Value.ShouldBe(expectedDisposable);
        }

        [Fact]
        public void WhenInitializedWithFactoryTryCreateLifetimeCreatesLifetimeWithValue()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(expectedDisposable), out var lifetime).ShouldBeTrue();

            lifetime.Value.ShouldBe(expectedDisposable);
        }

        [Fact]
        public void WhenInitializedWithFactoryValueIsCached()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(expectedDisposable), out var lifetime1).ShouldBeTrue();
            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(new Disposable()), out var lifetime2).ShouldBeTrue();

            lifetime2.Value.ShouldBe(expectedDisposable);
        }

        [Fact]
        public void WhenInitializedWithFactoryArgValueIsCached()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>();

            var factory1 = new ValueFactoryArg<int, int, Scoped<Disposable>>((k, v) => { expectedDisposable.State = v; return new Scoped<Disposable>(expectedDisposable); }, 1);
            var factory2 = new ValueFactoryArg<int, int, Scoped<Disposable>>((k, v) => { expectedDisposable.State = v; return new Scoped<Disposable>(expectedDisposable); }, 2);

            sa.TryCreateLifetime(1, factory1, out var lifetime1).ShouldBeTrue();
            sa.TryCreateLifetime(1, factory2, out var lifetime2).ShouldBeTrue();

            lifetime2.Value.ShouldBe(expectedDisposable);
            lifetime2.Value.State.ShouldBe(1);
        }

        [Fact]
        public void WhenScopeIsNotCreatedScopeIfCreatedReturnsNull()
        {
            var sa = new ScopedAtomicFactory<int, Disposable>();

            sa.ScopeIfCreated.ShouldBeNull();
        }

        [Fact]
        public void WhenScopeIsCreatedScopeIfCreatedReturnsScope()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>(expectedDisposable);

            sa.ScopeIfCreated.ShouldNotBeNull();
            sa.ScopeIfCreated.TryCreateLifetime(out var lifetime).ShouldBeTrue();
            lifetime.Value.ShouldBe(expectedDisposable);
        }

        [Fact]
        public void WhenNotInitTryCreateReturnsFalse()
        {
            var sa = new ScopedAtomicFactory<int, Disposable>();
            sa.TryCreateLifetime(out var l).ShouldBeFalse();
        }

        [Fact]
        public void WhenCreatedTryCreateLifetimeReturnsScope()
        {
            var expectedDisposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>(expectedDisposable);

            sa.TryCreateLifetime(out var lifetime).ShouldBeTrue();
            lifetime.Value.ShouldBe(expectedDisposable);
        }

        [Fact]
        public void WhenScopeDisposedTryCreateLifetimeReturnsFalse()
        {
            var sa = new ScopedAtomicFactory<int, Disposable>();
            sa.Dispose();

            sa.TryCreateLifetime(out var lifetime).ShouldBeFalse();
        }

        [Fact]
        public void WhenInitializedWithValueThenDisposedCreateLifetimeIsFalse()
        {
            var sa = new ScopedAtomicFactory<int, Disposable>(new Disposable());
            sa.Dispose();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(new Disposable()), out var l).ShouldBeFalse();
        }

        [Fact]
        public void WhenCreatedThenDisposedCreateLifetimeIsFalse()
        {
            var sa = new ScopedAtomicFactory<int, Disposable>();
            sa.Dispose();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(new Disposable()), out var l).ShouldBeFalse();
        }

        [Fact]
        public void WhenInitializedLifetimeKeepsValueAlive()
        {
            var disposable = new Disposable();
            var sa = new ScopedAtomicFactory<int, Disposable>();

            sa.TryCreateLifetime(1, k => new Scoped<Disposable>(disposable), out var lifetime1).ShouldBeTrue();
            sa.TryCreateLifetime(1, k => null, out var lifetime2).ShouldBeTrue();

            sa.Dispose();
            disposable.IsDisposed.ShouldBeFalse();

            lifetime1.Dispose();
            disposable.IsDisposed.ShouldBeFalse();

            lifetime2.Dispose();
            disposable.IsDisposed.ShouldBeTrue();
        }
    }
}
