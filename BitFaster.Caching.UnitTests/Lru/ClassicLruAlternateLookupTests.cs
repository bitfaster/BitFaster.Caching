#if NET9_0_OR_GREATER
using System;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ClassicLruAlternateLookupTests
    {
        [Fact]
        public void TryGetAlternateLookupReturnsLookupForCompatibleComparer()
        {
            var cache = new ClassicLru<string, string>(1, 3, StringComparer.Ordinal);
            cache.GetOrAdd("42", _ => "value");
            ReadOnlySpan<char> key = "42";

            cache.TryGetAlternateLookup<ReadOnlySpan<char>>(out var alternate).Should().BeTrue();
            alternate.TryGet(key, out var value).Should().BeTrue();
            value.Should().Be("value");
        }

        [Fact]
        public void GetAlternateLookupThrowsForIncompatibleComparer()
        {
            var cache = new ClassicLru<string, string>(1, 3, StringComparer.Ordinal);

            Action act = () => cache.GetAlternateLookup<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("Incompatible comparer");
            cache.TryGetAlternateLookup<int>(out var alternate).Should().BeFalse();
            alternate.Should().BeNull();
        }

        [Fact]
        public void AlternateLookupTryGetReturnsFalseForMissingKey()
        {
            var cache = new ClassicLru<string, string>(1, 3, StringComparer.Ordinal);
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryGet(key, out var value).Should().BeFalse();
            value.Should().BeNull();
        }

        [Fact]
        public void AlternateLookupTryRemoveReturnsActualKeyAndValue()
        {
            var cache = new ClassicLru<string, string>(1, 3, StringComparer.Ordinal);
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
            var cache = new ClassicLru<string, string>(1, 3, StringComparer.Ordinal);
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            ReadOnlySpan<char> key = "42";

            alternate.TryRemove(key, out var actualKey, out var value).Should().BeFalse();
            actualKey.Should().BeNull();
            value.Should().BeNull();
        }

        [Fact]
        public void AlternateLookupGetOrAddUsesAlternateKeyOnMissAndHit()
        {
            var cache = new ClassicLru<string, string>(1, 3, StringComparer.Ordinal);
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;
            ReadOnlySpan<char> key = "42";

            alternate.GetOrAdd(key, key =>
            {
                factoryCalls++;
                return $"value-{key.ToString()}";
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
        public void AlternateLookupTryUpdateReturnsFalseForMissingKeyAndUpdatesExistingValue()
        {
            var cache = new ClassicLru<string, string>(1, 3, StringComparer.Ordinal);
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
            var cache = new ClassicLru<string, string>(1, 3, StringComparer.Ordinal);
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

        [Fact]
        public void AlternateLookupGetOrAddWithArgUsesAlternateKeyOnMissAndHit()
        {
            var cache = new ClassicLru<string, string>(1, 3, StringComparer.Ordinal);
            var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            var factoryCalls = 0;
            ReadOnlySpan<char> key = "42";

            alternate.GetOrAdd(key, (k, prefix) =>
            {
                factoryCalls++;
                return $"{prefix}-{k.ToString()}";
            }, "value").Should().Be("value-42");

            alternate.GetOrAdd(key, (_, prefix) =>
            {
                factoryCalls++;
                return prefix;
            }, "unused").Should().Be("value-42");

            factoryCalls.Should().Be(1);
        }
    }
}
#endif
