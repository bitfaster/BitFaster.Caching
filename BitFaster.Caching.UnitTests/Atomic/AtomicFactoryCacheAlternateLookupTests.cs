#if NET9_0_OR_GREATER
using System;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AtomicFactoryCacheAlternateLookupTests
    {
        private readonly AtomicFactoryCache<string, string> cache;

        public AtomicFactoryCacheAlternateLookupTests()
        {
            var innerCache = new ConcurrentLru<string, AtomicFactory<string, string>>(1, 9, StringComparer.Ordinal);
            cache = new AtomicFactoryCache<string, string>(innerCache);
        }

        [Fact]
        public void TryGetAlternateLookupReturnsLookupForCompatibleComparer()
        {
            cache.GetOrAdd("42", _ => "value");
            ReadOnlySpan<char> key = "42";

            cache.TryGetAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeTrue();
            alternate.TryGet(key, out var value).Should().BeTrue();
            value.Should().Be("value");
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
        public void AlternateLookupTryGetReturnsFalseForMissingKey()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryGet(key, out _).Should().BeFalse();
        }

        [Fact]
        public void AlternateLookupTryRemoveReturnsActualKeyAndValue()
        {
            cache.GetOrAdd("42", _ => "value");
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key, out var actualKey, out var value).Should().BeTrue();

            actualKey.Should().Be("42");
            value.Should().Be("value");
            cache.TryGet("42", out _).Should().BeFalse();
        }

        [Fact]
        public void AlternateLookupTryRemoveReturnsFalseForMissingKey()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key, out _, out _).Should().BeFalse();
        }

        [Fact]
        public void AlternateLookupGetOrAddUsesAlternateKeyOnMissAndHit()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;
            ReadOnlySpan<char> key = "42";

            alternate.GetOrAdd(key, key =>
            {
                factoryCalls++;
                return $"value-{key}";
            }).Should().Be("value-42");

            alternate.GetOrAdd(key, (_, prefix) =>
            {
                factoryCalls++;
                return prefix;
            }, "unused").Should().Be("value-42");

            factoryCalls.Should().Be(1);
            cache.TryGet("42", out var value).Should().BeTrue();
            value.Should().Be("value-42");
        }

        [Fact]
        public void AlternateLookupGetOrAddWithArgUsesAlternateKeyOnMissAndHit()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;
            ReadOnlySpan<char> key = "42";

            alternate.GetOrAdd(key, (k, prefix) =>
            {
                factoryCalls++;
                return $"{prefix}-{k}";
            }, "value").Should().Be("value-42");

            alternate.GetOrAdd(key, (_, prefix) =>
            {
                factoryCalls++;
                return prefix;
            }, "unused").Should().Be("value-42");

            factoryCalls.Should().Be(1);
            cache.TryGet("42", out var value).Should().BeTrue();
            value.Should().Be("value-42");
        }

        [Fact]
        public void AlternateLookupTryUpdateReturnsFalseForMissingKeyAndUpdatesExistingValue()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
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
        public void AlternateLookupAddOrUpdateAddsMissingValueAndUpdatesExistingValue()
        {
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
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
