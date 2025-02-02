using Shouldly;
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
            telemetryPolicy.Total.ShouldBe(0);
            telemetryPolicy.IncrementHit();
            telemetryPolicy.Total.ShouldBe(1);
        }

        [Fact]
        public void WhenHitHitsIs1()
        {
            telemetryPolicy.Hits.ShouldBe(0);
            telemetryPolicy.IncrementHit();
            telemetryPolicy.Hits.ShouldBe(1);
        }

        [Fact]
        public void WhenMissMissesIs1()
        {
            telemetryPolicy.Misses.ShouldBe(0);
            telemetryPolicy.IncrementMiss();
            telemetryPolicy.Misses.ShouldBe(1);
        }

        [Fact]
        public void WhenHitCountAndTotalCountAreEqualRatioIs1()
        {
            telemetryPolicy.IncrementHit();

            telemetryPolicy.HitRatio.ShouldBe(1.0);
        }

        [Fact]
        public void WhenHitCountIsEqualToMissCountRatioIsHalf()
        {
            telemetryPolicy.IncrementMiss();
            telemetryPolicy.IncrementHit();

            telemetryPolicy.HitRatio.ShouldBe(0.5);
        }

        [Fact]
        public void WhenTotalCountIsZeroRatioReturnsZero()
        {
            telemetryPolicy.HitRatio.ShouldBe(0.0);
        }

        [Fact]
        public void WhenItemUpdatedIncrementUpdatedCount()
        {
            telemetryPolicy.OnItemUpdated(1, 2, 3);

            telemetryPolicy.Updated.ShouldBe(1);
        }

        [Fact]
        public void WhenItemRemovedIncrementEvictedCount()
        {
            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            telemetryPolicy.Evicted.ShouldBe(1);
        }

        [Fact]
        public void WhenItemRemovedDontIncrementEvictedCount()
        {
            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Removed);

            telemetryPolicy.Evicted.ShouldBe(0);
        }

        [Fact]
        public void WhenOnItemRemovedInvokedEventIsFired()
        {
            List<ItemRemovedEventArgs<int, int>> eventList = new();

            telemetryPolicy.ItemRemoved += (source, args) => eventList.Add(args);

            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            eventList.Count.ShouldBe(1);
            eventList[0].Key.ShouldBe(1);
            eventList[0].Value.ShouldBe(2);
            eventList[0].Reason.ShouldBe(ItemRemovedReason.Evicted);
        }

        [Fact]
        public void WhenEventSourceIsSetItemRemovedEventUsesSource()
        {
            List<object> eventSourceList = new();

            telemetryPolicy.SetEventSource(this);

            telemetryPolicy.ItemRemoved += (source, args) => eventSourceList.Add(source);

            telemetryPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            eventSourceList.Count.ShouldBe(1);
            eventSourceList[0].ShouldBe(this);
        }

        [Fact]
        public void WhenOnItemUpdatedInvokedEventIsFired()
        {
            List<ItemUpdatedEventArgs<int, int>> eventList = new();

            telemetryPolicy.ItemUpdated += (source, args) => eventList.Add(args);

            telemetryPolicy.OnItemUpdated(1, 2, 3);

            eventList.Count.ShouldBe(1);
            eventList[0].Key.ShouldBe(1);
            eventList[0].OldValue.ShouldBe(2);
            eventList[0].NewValue.ShouldBe(3);
        }

        [Fact]
        public void WhenEventSourceIsSetItemUpdatedEventUsesSource()
        {
            List<object> eventSourceList = new();

            telemetryPolicy.SetEventSource(this);

            telemetryPolicy.ItemUpdated += (source, args) => eventSourceList.Add(source);

            telemetryPolicy.OnItemUpdated(1, 2, 3);

            eventSourceList.Count.ShouldBe(1);
            eventSourceList[0].ShouldBe(this);
        }

// backcompat: remove 
#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void WhenInterfaceDefaultItemUpdatedRegisteredNoOp()
        {
            var policy = new Mock<ITelemetryPolicy<int, int>>();
            policy.CallBase = true;

            Action act = () => policy.Object.OnItemUpdated(1, 2, 3);

            act.ShouldNotThrow();
        }
#endif
    }
}
