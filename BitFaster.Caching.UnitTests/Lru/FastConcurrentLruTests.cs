using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class FastConcurrentLruTests
    {
        [Fact]
        public void ConstructAddAndRetrieveWithCustomComparerReturnsValue()
        {
            var lru = new FastConcurrentLru<string, int>(9, 9, StringComparer.OrdinalIgnoreCase);

            lru.GetOrAdd("foo", k => 1);

            lru.TryGet("FOO", out var value).Should().BeTrue();
            value.Should().Be(1);
        }

        [Fact]
        public void ConstructAddAndRetrieveWithDefaultCtorReturnsValue()
        {
            var x = new FastConcurrentLru<int, int>(3);

            x.GetOrAdd(1, k => k).Should().Be(1);
        }
    }
}
