using FluentAssertions;
using Lightweight.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Lightweight.Caching.UnitTests.Lru
{
    public class FastConcurrentTLruTests
    {
        [Fact]
        public void ConstructAddAndRetrieveWithCustomComparerReturnsValue()
        {
            var lru = new FastConcurrentTLru<string, int>(9, 9, StringComparer.OrdinalIgnoreCase, TimeSpan.FromSeconds(10));

            lru.GetOrAdd("foo", k => 1);

            lru.TryGet("FOO", out var value).Should().BeTrue();
            value.Should().Be(1);
        }
    }
}
