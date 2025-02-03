using Shouldly;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
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

            lru.TryGet("FOO", out var value).ShouldBeTrue();
            value.ShouldBe(1);
        }

        [Fact]
        public void ConstructWithDefaultCtorReturnsCapacity()
        {
            var x = new FastConcurrentLru<int, int>(3);

            x.Capacity.ShouldBe(3);
        }

        [Fact]
        public void ConstructPartitionCtorReturnsCapacity()
        {
            var x = new FastConcurrentLru<int, int>(1, new EqualCapacityPartition(3), EqualityComparer<int>.Default);

            x.Capacity.ShouldBe(3);
        }

        [Fact]
        public void MetricsHasValueIsFalse()
        {
            var x = new FastConcurrentLru<int, int>(3);

            x.Metrics.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void EventsHasValueIsFalse()
        {
            var x = new FastConcurrentLru<int, int>(3);

            x.Events.HasValue.ShouldBeFalse();
        }
    }
}
