using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class TelemetryPolicyTests
    {
        private TelemetryPolicy<int, int> telemetryPolicy = default;

        public TelemetryPolicyTests()
        {
            telemetryPolicy.SetEventSource(this);
        }

        [Fact]
        public void WhenHitTotalIs1()
        {
            telemetryPolicy.Total.Should().Be(0);
            telemetryPolicy.IncrementHit();
            telemetryPolicy.Total.Should().Be(1);
        }

        [Fact]
        public void WhenHitHitsIs1()
        {
            telemetryPolicy.Hits.Should().Be(0);
            telemetryPolicy.IncrementHit();
            telemetryPolicy.Hits.Should().Be(1);
        }

        [Fact]
        public void WhenMissMissesIs1()
        {
            telemetryPolicy.Misses.Should().Be(0);
            telemetryPolicy.IncrementMiss();
            telemetryPolicy.Misses.Should().Be(1);
        }

        [Fact]
        public void WhenHitCountAndTotalCountAreEqualRatioIs1()
        {
            telemetryPolicy.IncrementHit();

            telemetryPolicy.HitRatio.Should().Be(1.0);
        }

        [Fact]
        public void WhenHitCountIsEqualToMissCountRatioIsHalf()
        {
            telemetryPolicy.IncrementMiss();
            telemetryPolicy.IncrementHit();

            telemetryPolicy.HitRatio.Should().Be(0.5);
        }

        [Fact]
        public void WhenTotalCountIsZeroRatioReturnsZero()
        {
            telemetryPolicy.HitRatio.Should().Be(0.0);
        }

        [Fact]
        public void WhenItemUpdatedIncrementUpdatedCount()
        {
            telemetryPolicy.OnItemUpdated(1, 2);

            telemetryPolicy.Updated.Should().Be(1);
        }

        [Fact]
        public void WhenItemRemovedIncrementEvictedCount()
        {
            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            telemetryPolicy.Evicted.Should().Be(1);
        }

        [Fact]
        public void WhenItemRemovedDontIncrementEvictedCount()
        {
            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Removed);

            telemetryPolicy.Evicted.Should().Be(0);
        }

        [Fact]
        public void WhenOnItemRemovedInvokedEventIsFired()
        {
            var eventList = new List<ItemRemovedEventArgs<int, int>>();

            telemetryPolicy.ItemRemoved += (source, args) => eventList.Add(args);

            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            eventList.Should().HaveCount(1);
            eventList[0].Key.Should().Be(1);
            eventList[0].Value.Should().Be(2);
            eventList[0].Reason.Should().Be(ItemRemovedReason.Evicted);
        }

        [Fact]
        public void WhenEventSourceIsSetItemRemovedEventUsesSource()
        {
            List<object> eventSourceList = new List<object>();

            telemetryPolicy.SetEventSource(this);

            telemetryPolicy.ItemRemoved += (source, args) => eventSourceList.Add(source);

            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            eventSourceList.Should().HaveCount(1);
            eventSourceList[0].Should().Be(this);
        }
    }
}
