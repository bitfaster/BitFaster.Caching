#if NET9_0_OR_GREATER
using System;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AtomicFactoryScopedAsyncCacheAlternateLookupTests
    {
        private readonly AtomicFactoryScopedAsyncCache<string, Disposable> cache;

        public AtomicFactoryScopedAsyncCacheAlternateLookupTests()
        {
            cache = new AtomicFactoryScopedAsyncCache<string, Disposable>(
                new ConcurrentLru<string, ScopedAsyncAtomicFactory<string, Disposable>>(1, 6, StringComparer.Ordinal));
        }

        [Fact]
        public void TryGetAlternateLookupReturnsLookupForCompatibleComparer()
        {
            cache.TryGetAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeTrue();
            alternate.Should().NotBeNull();
        }

        [Fact]
        public void GetAlternateLookupThrowsForIncompatibleComparer()
        {
            Action act = () => cache.GetAlternateLookup<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("Incompatible comparer");
            cache.TryGetAlternateLookup<int>(out var alternate).Should().BeFalse();
            alternate.Should().BeNull();
        }

        [Fact]
        public void ScopedTryGetReturnsFalseWhenKeyDoesNotExist()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.ScopedTryGet(key, out var lifetime).Should().BeFalse();
        }

        [Fact]
        public void ScopedTryGetReturnsLifetimeWhenKeyExists()
        {
            var disposable = new Disposable();
            cache.AddOrUpdate("42", disposable);
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.ScopedTryGet(key, out var lifetime).Should().BeTrue();
            lifetime.Value.Should().Be(disposable);
        }

        [Fact]
        public void TryRemoveReturnsFalseWhenKeyDoesNotExist()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key).Should().BeFalse();
        }

        [Fact]
        public void TryRemoveReturnsTrueAndRemovesExistingEntry()
        {
            cache.AddOrUpdate("42", new Disposable());
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key).Should().BeTrue();
            cache.ScopedTryGet("42", out _).Should().BeFalse();
        }

        [Fact]
        public void TryUpdateReturnsFalseWhenKeyDoesNotExist()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryUpdate(key, new Disposable()).Should().BeFalse();
        }

        [Fact]
        public void TryUpdateReturnsTrueAndUpdatesExistingEntry()
        {
            var d1 = new Disposable(1);
            var d2 = new Disposable(2);
            cache.AddOrUpdate("42", d1);
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryUpdate(key, d2).Should().BeTrue();

            cache.ScopedTryGet("42", out var lifetime).Should().BeTrue();
            lifetime.Value.State.Should().Be(2);
        }

        [Fact]
        public void AddOrUpdateAddsNewEntryWhenKeyDoesNotExist()
        {
            var disposable = new Disposable();
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.AddOrUpdate(key, disposable);

            cache.ScopedTryGet("42", out var lifetime).Should().BeTrue();
            lifetime.Value.Should().Be(disposable);
        }

        [Fact]
        public void AddOrUpdateUpdatesExistingEntry()
        {
            var d1 = new Disposable(1);
            var d2 = new Disposable(2);
            cache.AddOrUpdate("42", d1);
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.AddOrUpdate(key, d2);

            cache.ScopedTryGet("42", out var lifetime).Should().BeTrue();
            lifetime.Value.State.Should().Be(2);
        }

        [Fact]
        public async Task ScopedGetOrAddAsyncReturnsExistingValue()
        {
            var disposable = new Disposable();
            cache.AddOrUpdate("42", disposable);
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            using var lifetime = await alternate.ScopedGetOrAddAsync(key, k => Task.FromResult(new Scoped<Disposable>(new Disposable())));

            lifetime.Value.Should().Be(disposable);
        }

        [Fact]
        public async Task ScopedGetOrAddAsyncAddsNewValue()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            using var lifetime = await alternate.ScopedGetOrAddAsync(key, k => Task.FromResult(new Scoped<Disposable>(new Disposable())));

            lifetime.Value.Should().NotBeNull();
            cache.ScopedTryGet("42", out var lifetime2).Should().BeTrue();
        }

        [Fact]
        public async Task ScopedGetOrAddAsyncWithArgReturnsExistingValue()
        {
            var disposable = new Disposable();
            cache.AddOrUpdate("42", disposable);
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            using var lifetime = await alternate.ScopedGetOrAddAsync(
                key,
                (k, arg) => Task.FromResult(new Scoped<Disposable>(new Disposable(arg))),
                99);

            lifetime.Value.Should().Be(disposable);
        }

        [Fact]
        public async Task ScopedGetOrAddAsyncWithArgAddsNewValue()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            using var lifetime = await alternate.ScopedGetOrAddAsync(
                key,
                (k, arg) => Task.FromResult(new Scoped<Disposable>(new Disposable(arg))),
                99);

            lifetime.Value.State.Should().Be(99);
            cache.ScopedTryGet("42", out var lifetime2).Should().BeTrue();
        }

        [Fact]
        public void WhenInnerCacheDoesNotSupportAlternateLookupTryGetReturnsFalse()
        {
            var mockCache = new Moq.Mock<ICache<string, ScopedAsyncAtomicFactory<string, Disposable>>>();
            mockCache.SetupGet(c => c.Events).Returns(Optional<ICacheEvents<string, ScopedAsyncAtomicFactory<string, Disposable>>>.None());
            var wrapper = new AtomicFactoryScopedAsyncCache<string, Disposable>(mockCache.Object);

            wrapper.TryGetAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeFalse();
            alternate.Should().BeNull();
        }

        [Fact]
        public void WhenInnerCacheDoesNotSupportAlternateLookupGetAlternateLookupThrows()
        {
            var mockCache = new Moq.Mock<ICache<string, ScopedAsyncAtomicFactory<string, Disposable>>>();
            mockCache.SetupGet(c => c.Events).Returns(Optional<ICacheEvents<string, ScopedAsyncAtomicFactory<string, Disposable>>>.None());
            var wrapper = new AtomicFactoryScopedAsyncCache<string, Disposable>(mockCache.Object);

            Action act = () => wrapper.GetAlternateLookup<ReadOnlySpan<char>>();
            act.Should().Throw<InvalidOperationException>();
        }
    }
}
#endif
