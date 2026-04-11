#if NET9_0_OR_GREATER
using System;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AtomicFactoryAsyncCacheAsyncAlternateLookupTests
    {
        private readonly AtomicFactoryAsyncCache<string, string> cache;

        public AtomicFactoryAsyncCacheAsyncAlternateLookupTests()
        {
            var innerCache = new ConcurrentLru<string, AsyncAtomicFactory<string, string>>(1, 9, StringComparer.Ordinal);
            cache = new AtomicFactoryAsyncCache<string, string>(innerCache);
        }

        [Fact]
        public void TryGetAsyncAlternateLookupReturnsLookupForCompatibleComparer()
        {
            cache.AddOrUpdate("42", "value");
            ReadOnlySpan<char> key = "42";

            cache.TryGetAsyncAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeTrue();
            alternate.TryGet(key, out var value).Should().BeTrue();
            value.Should().Be("value");
        }

        [Fact]
        public void GetAsyncAlternateLookupThrowsForIncompatibleComparer()
        {
            Action act = () => cache.GetAsyncAlternateLookup<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("Incompatible comparer");
            cache.TryGetAsyncAlternateLookup<int>(out var alternate).Should().BeFalse();
            alternate.Should().BeNull();
        }

        [Fact]
        public void AsyncAlternateLookupTryGetReturnsFalseForMissingKey()
        {
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryGet(key, out _).Should().BeFalse();
        }

        [Fact]
        public void AsyncAlternateLookupTryRemoveReturnsActualKeyAndValue()
        {
            cache.AddOrUpdate("42", "value");
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key, out var actualKey, out var value).Should().BeTrue();

            actualKey.Should().Be("42");
            value.Should().Be("value");
            cache.TryGet("42", out _).Should().BeFalse();
        }

        [Fact]
        public void AsyncAlternateLookupTryRemoveReturnsFalseForMissingKey()
        {
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key, out _, out _).Should().BeFalse();
        }

        [Fact]
        public void AsyncAlternateLookupTryUpdateReturnsFalseForMissingKeyAndUpdatesExistingValue()
        {
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryUpdate(key, "value-42").Should().BeFalse();
            cache.TryGet("42", out _).Should().BeFalse();

            cache.AddOrUpdate("42", "value-42");
            alternate.TryUpdate(key, "updated").Should().BeTrue();

            cache.TryGet("42", out var value).Should().BeTrue();
            value.Should().Be("updated");
            alternate.TryGet(key, out value).Should().BeTrue();
            value.Should().Be("updated");
        }

        [Fact]
        public void AsyncAlternateLookupAddOrUpdateAddsMissingValueAndUpdatesExistingValue()
        {
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.AddOrUpdate(key, "value-42");

            cache.TryGet("42", out var value).Should().BeTrue();
            value.Should().Be("value-42");

            alternate.AddOrUpdate(key, "updated");

            cache.TryGet("42", out value).Should().BeTrue();
            value.Should().Be("updated");
            alternate.TryGet(key, out value).Should().BeTrue();
            value.Should().Be("updated");
        }

        [Fact]
        public async Task AsyncAlternateLookupGetOrAddAsyncUsesActualKeyOnMissAndHit()
        {
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;

            var result = await alternate.GetOrAddAsync("42".AsSpan(), key =>
            {
                factoryCalls++;
                return Task.FromResult($"value-{key}");
            });
            result.Should().Be("value-42");

            result = await alternate.GetOrAddAsync("42".AsSpan(), key =>
            {
                factoryCalls++;
                return Task.FromResult("unused");
            });
            result.Should().Be("value-42");

            factoryCalls.Should().Be(1);
            cache.TryGet("42", out var value).Should().BeTrue();
            value.Should().Be("value-42");
        }

        [Fact]
        public async Task AsyncAlternateLookupGetOrAddAsyncWithArgUsesActualKeyOnMissAndHit()
        {
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;

            var result = await alternate.GetOrAddAsync("42".AsSpan(), (key, prefix) =>
            {
                factoryCalls++;
                return Task.FromResult($"{prefix}{key}");
            }, "value-");
            result.Should().Be("value-42");

            result = await alternate.GetOrAddAsync("42".AsSpan(), (key, prefix) =>
            {
                factoryCalls++;
                return Task.FromResult("unused");
            }, "unused-");
            result.Should().Be("value-42");

            factoryCalls.Should().Be(1);
            cache.TryGet("42", out var value).Should().BeTrue();
            value.Should().Be("value-42");
        }
    }
}
#endif
