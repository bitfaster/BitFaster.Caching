using FluentAssertions;
using BitFaster.Caching.Lru;
using System.Collections.Generic;
using Xunit;
using Moq;
using System;

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
            telemetryPolicy.OnItemUpdated(1, 2, 3);

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

            telemetryPolicy.SetEventSource(this);

            telemetryPolicy.ItemRemoved += (source, args) => eventSourceList.Add(source);

            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            eventSourceList.Should().HaveCount(1);
            eventSourceList[0].Should().Be(this);
        }

        [Fact]
        public void WhenOnItemUpdatedInvokedEventIsFired()
        {
            List<ItemUpdatedEventArgs<int, int>> eventList = new();

            telemetryPolicy.ItemUpdated += (source, args) => eventList.Add(args);

            telemetryPolicy.OnItemUpdated(1, 2, 3);

            eventList.Should().HaveCount(1);
            eventList[0].Key.Should().Be(1);
            eventList[0].OldValue.Should().Be(2);
            eventList[0].NewValue.Should().Be(3);
        }

        [Fact]
        public void WhenEventSourceIsSetItemUpdatedEventUsesSource()
        {
            List<object> eventSourceList = new();

            telemetryPolicy.SetEventSource(this);

            telemetryPolicy.ItemUpdated += (source, args) => eventSourceList.Add(source);

            telemetryPolicy.OnItemUpdated(1, 2, 3);

            eventSourceList.Should().HaveCount(1);
            eventSourceList[0].Should().Be(this);
        }

// backcompat: remove 
#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void WhenInterfaceDefaultItemUpdatedRegisteredNoOp()
        {
            var policy = new Mock<ITelemetryPolicy<int, int>>();
            policy.CallBase = true;

            Action act = () => policy.Object.OnItemUpdated(1, 2, 3);

            act.Should().NotThrow();
        }
#endif
    }
}
