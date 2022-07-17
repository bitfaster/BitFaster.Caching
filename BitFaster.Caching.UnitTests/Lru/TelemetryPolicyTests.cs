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
        public void IsEnabledIsTrue()
        {
            telemetryPolicy.IsEnabled.Should().BeTrue();
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
        public void WhenOnItemRemovedInvokedEventIsFired()
        {
            List<ItemRemovedEventArgs<int, int>> eventList = new();

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
            List<object> eventSourceList = new();

            telemetryPolicy.ItemRemoved += (source, args) => eventSourceList.Add(source);

            telemetryPolicy.SetEventSource(this);
            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            eventSourceList.Should().HaveCount(1);
            eventSourceList[0].Should().Be(this);
        }
    }
}
