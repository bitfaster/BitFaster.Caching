#if NET9_0_OR_GREATER
using System;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentLruAsyncAlternateLookupTests
    {
        [Fact]
        public void TryGetAsyncAlternateLookupReturnsLookupForCompatibleComparer()
        {
            var cache = new ConcurrentLru<string, string>(1, 3, StringComparer.Ordinal);
            cache.GetOrAdd("42", _ => "value");
            ReadOnlySpan<char> key = "42";

            cache.TryGetAsyncAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeTrue();
            alternate.TryGet(key, out var value).Should().BeTrue();
            value.Should().Be("value");
        }

        [Fact]
        public void GetAsyncAlternateLookupThrowsForIncompatibleComparer()
        {
            var cache = new ConcurrentLru<string, string>(1, 3, StringComparer.Ordinal);

            Action act = () => cache.GetAsyncAlternateLookup<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("Incompatible comparer");
            cache.TryGetAsyncAlternateLookup<int>(out var alternate).Should().BeFalse();
            alternate.Should().BeNull();
        }

        [Fact]
        public void AsyncAlternateLookupTryRemoveReturnsActualKeyAndValue()
        {
            var cache = new ConcurrentLru<string, string>(1, 3, StringComparer.Ordinal);
            cache.GetOrAdd("42", _ => "value");
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key, out var actualKey, out var value).Should().BeTrue();

            actualKey.Should().Be("42");
            value.Should().Be("value");
            cache.TryGet("42", out _).Should().BeFalse();
        }

        [Fact]
        public async Task AsyncAlternateLookupGetOrAddAsyncUsesAlternateKeyOnMissAndHit()
        {
            var cache = new ConcurrentLru<string, string>(1, 3, StringComparer.Ordinal);
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;

            var result = await alternate.GetOrAddAsync("42".AsSpan(), key =>
            {
                factoryCalls++;
                return Task.FromResult($"value-{key.ToString()}");
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
        public async Task AsyncAlternateLookupGetOrAddAsyncWithArgUsesAlternateKeyOnMissAndHit()
        {
            var cache = new ConcurrentLru<string, string>(1, 3, StringComparer.Ordinal);
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;

            var result = await alternate.GetOrAddAsync("42".AsSpan(), (key, prefix) =>
            {
                factoryCalls++;
                return Task.FromResult($"{prefix}{key.ToString()}");
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

        [Fact]
        public void AsyncAlternateLookupGetOrAddWithArgUsesAlternateKeyOnMissAndHit()
        {
            var cache = new ConcurrentLru<string, string>(1, 3, StringComparer.Ordinal);
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;
            ReadOnlySpan<char> key = "42";

            alternate.GetOrAdd(key, (key, prefix) =>
            {
                factoryCalls++;
                return $"{prefix}{key.ToString()}";
            }, "value-").Should().Be("value-42");

            alternate.GetOrAdd(key, (key, prefix) =>
            {
                factoryCalls++;
                return "unused";
            }, "unused-").Should().Be("value-42");

            factoryCalls.Should().Be(1);
            cache.TryGet("42", out var value).Should().BeTrue();
            value.Should().Be("value-42");
        }

        [Fact]
        public void AsyncAlternateLookupTryUpdateReturnsFalseForMissingKeyAndUpdatesExistingValue()
        {
            var cache = new ConcurrentLru<string, string>(1, 3, StringComparer.Ordinal);
            var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryUpdate(key, "value-42").Should().BeFalse();
            cache.TryGet("42", out _).Should().BeFalse();

            cache.GetOrAdd("42", _ => "value-42");
            alternate.TryUpdate(key, "updated").Should().BeTrue();

            cache.TryGet("42", out var value).Should().BeTrue();
            value.Should().Be("updated");
            alternate.TryGet(key, out value).Should().BeTrue();
            value.Should().Be("updated");
        }

        [Fact]
        public void AsyncAlternateLookupAddOrUpdateAddsMissingValueAndUpdatesExistingValue()
        {
            var cache = new ConcurrentLru<string, string>(1, 3, StringComparer.Ordinal);
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
    }
}
#endif
