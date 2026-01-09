using System;
using System.Collections.Generic;
using BitFaster.Caching.Lru;
using FluentAssertions;
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
        public void ConstructWithDefaultCtorReturnsCapacity()
        {
            var x = new FastConcurrentLru<int, int>(3);

            x.Capacity.Should().Be(3);
        }

        [Fact]
        public void ConstructPartitionCtorReturnsCapacity()
        {
            var x = new FastConcurrentLru<int, int>(1, new EqualCapacityPartition(3), EqualityComparer<int>.Default);

            x.Capacity.Should().Be(3);
        }

        [Fact]
        public void MetricsHasValueIsFalse()
        {
            var x = new FastConcurrentLru<int, int>(3);

            x.Metrics.HasValue.Should().BeFalse();
        }

        [Fact]
        public void EventsHasValueIsFalse()
        {
            var x = new FastConcurrentLru<int, int>(3);

            x.Events.HasValue.Should().BeFalse();
        }
    }
}
