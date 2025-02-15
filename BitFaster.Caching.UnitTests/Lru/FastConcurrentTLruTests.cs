using Shouldly;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using Xunit;
using System.Threading.Tasks;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class FastConcurrentTLruTests
    {
        [Fact]
        public void ConstructAddAndRetrieveWithCustomComparerReturnsValue()
        {
            var lru = new FastConcurrentTLru<string, int>(9, 9, StringComparer.OrdinalIgnoreCase, TimeSpan.FromSeconds(10));

            lru.GetOrAdd("foo", k => 1);

            lru.TryGet("FOO", out var value).ShouldBeTrue();
            value.ShouldBe(1);
        }

        [Fact]
        public void ConstructWithDefaultCtorReturnsCapacity()
        {
            var x = new FastConcurrentTLru<int, int>(3, TimeSpan.FromSeconds(1));

            x.Capacity.ShouldBe(3);
        }

        [Fact]
        public void ConstructPartitionCtorReturnsCapacity()
        {
            var x = new FastConcurrentTLru<int, int>(1, new EqualCapacityPartition(3), EqualityComparer<int>.Default, TimeSpan.FromSeconds(1));

            x.Capacity.ShouldBe(3);
        }

        [Fact]
        public async Task WhenItemsAreExpiredExpireRemovesExpiredItems()
        {
            var ttl = TimeSpan.FromMilliseconds(10);
            var lru = new FastConcurrentTLru<int, string>(9, 9, EqualityComparer<int>.Default, ttl);

            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");

            await Task.Delay(ttl.MultiplyBy(2));

            lru.Policy.ExpireAfterWrite.Value.TrimExpired();

            lru.Count.ShouldBe(0);
        }

        [Fact]
        public void MetricsHasValueIsFalse()
        {
            var x = new FastConcurrentTLru<int, int>(3, TimeSpan.FromSeconds(1));

            x.Metrics.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void EventsHasValueIsFalse()
        {
            var x = new FastConcurrentTLru<int, int>(3, TimeSpan.FromSeconds(1));

            x.Events.HasValue.ShouldBeFalse();
        }
    }
}
