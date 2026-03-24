#if NET9_0_OR_GREATER
using System;
using System.Collections.Generic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentLruAlternateCacheTests
    {
        [Fact]
        public void TryGetAlternateCacheReturnsLookupForCompatibleComparer()
        {
            var comparer = new AlternateIntStringComparer();
            var cache = new ConcurrentLru<string, string>(1, 3, comparer);
            cache.GetOrAdd("42", _ => "value");

            cache.TryGetAlternateCache<int>(out var alternate).Should().BeTrue();
            alternate.TryGet(42, out var value).Should().BeTrue();
            value.Should().Be("value");
            comparer.CreateCallCount.Should().Be(0);
        }

        [Fact]
        public void GetAlternateCacheThrowsForIncompatibleComparer()
        {
            var cache = new ConcurrentLru<string, string>(1, 3, StringComparer.Ordinal);

            Action act = () => cache.GetAlternateCache<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("Incompatible comparer");
            cache.TryGetAlternateCache<int>(out var alternate).Should().BeFalse();
            alternate.Should().BeNull();
        }

        [Fact]
        public void AlternateCacheTryRemoveReturnsActualKeyAndValue()
        {
            var comparer = new AlternateIntStringComparer();
            var cache = new ConcurrentLru<string, string>(1, 3, comparer);
            cache.GetOrAdd("42", _ => "value");
            var alternate = cache.GetAlternateCache<int>();

            alternate.TryRemove(42, out var actualKey, out var value).Should().BeTrue();

            actualKey.Should().Be("42");
            value.Should().Be("value");
            cache.TryGet("42", out _).Should().BeFalse();
        }

        [Fact]
        public void AlternateCacheGetOrAddUsesAlternateComparerCreateOnlyOnMiss()
        {
            var comparer = new AlternateIntStringComparer();
            var cache = new ConcurrentLru<string, string>(1, 3, comparer);
            var alternate = cache.GetAlternateCache<int>();
            var factoryCalls = 0;

            alternate.GetOrAdd(42, key =>
            {
                factoryCalls++;
                return $"value-{key}";
            }).Should().Be("value-42");

            alternate.GetOrAdd(42, (_, prefix) =>
            {
                factoryCalls++;
                return prefix;
            }, "unused").Should().Be("value-42");

            factoryCalls.Should().Be(1);
            comparer.CreateCallCount.Should().Be(1);
        }

        private sealed class AlternateIntStringComparer : IEqualityComparer<string>, IAlternateEqualityComparer<int, string>
        {
            public int CreateCallCount { get; private set; }

            public string Create(int alternate)
            {
                this.CreateCallCount++;
                return alternate.ToString();
            }

            public bool Equals(int alternate, string other)
            {
                return StringComparer.Ordinal.Equals(alternate.ToString(), other);
            }

            public int GetHashCode(int alternate)
            {
                return StringComparer.Ordinal.GetHashCode(alternate.ToString());
            }

            public bool Equals(string x, string y)
            {
                return StringComparer.Ordinal.Equals(x, y);
            }

            public int GetHashCode(string obj)
            {
                return StringComparer.Ordinal.GetHashCode(obj);
            }
        }
    }
}
#endif
