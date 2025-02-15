﻿#if NETCOREAPP3_1_OR_GREATER

using Shouldly;
using BitFaster.Caching.Lru;
using System;
using System.Threading.Tasks;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class TLruTickCount64PolicyTests
    {
        // backcompat: change type to TLruTickCount64Policy
        private readonly TLruLongTicksPolicy<int, int> policy = new TLruLongTicksPolicy<int, int>(TimeSpan.FromSeconds(10));

        [Fact]
        public void WhenTtlIsTimeSpanMaxThrow()
        {
            Action constructor = () => { new TLruLongTicksPolicy<int, int>(TimeSpan.MaxValue); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsZeroThrow()
        {
            Action constructor = () => { new TLruLongTicksPolicy<int, int>(TimeSpan.Zero); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsMaxSetAsMax()
        {
            var policy = new TLruLongTicksPolicy<int, int>(Duration.MaxRepresentable);
            policy.TimeToLive.ShouldBe(Duration.MaxRepresentable, TimeSpan.FromMilliseconds(20));
        }

        [Fact]
        public void TimeToLiveShouldBeTenSecs()
        {
            this.policy.TimeToLive.ShouldBe(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void CreateItemInitializesKeyAndValue()
        {
            var item = this.policy.CreateItem(1, 2);

            item.Key.ShouldBe(1);
            item.Value.ShouldBe(2);
        }

        [Fact]
        public void CreateItemInitializesTimestampToNow()
        {
            var item = this.policy.CreateItem(1, 2);

            item.TickCount.ShouldBe(Duration.SinceEpoch().raw);
        }

        [Fact]
        public void TouchUpdatesItemWasAccessed()
        {
            var item = this.policy.CreateItem(1, 2);
            item.WasAccessed = false;

            this.policy.Touch(item);

            item.WasAccessed.ShouldBeTrue();
        }

        [Fact]
        public async Task UpdateUpdatesTickCount()
        {
            var item = this.policy.CreateItem(1, 2);
            var tc = item.TickCount;

            await Task.Delay(TimeSpan.FromMilliseconds(1));

            this.policy.Update(item);

            item.TickCount.ShouldBeGreaterThan(tc);
        }

        [Fact]
        public void WhenItemIsExpiredShouldDiscardIsTrue()
        {
            var item = this.policy.CreateItem(1, 2);
            item.TickCount = Duration.SinceEpoch().raw - Duration.FromSeconds(11).raw;

            this.policy.ShouldDiscard(item).ShouldBeTrue();
        }

        [Fact]
        public void WhenItemIsNotExpiredShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);
            item.TickCount = Duration.SinceEpoch().raw - Duration.FromSeconds(9).raw;

            this.policy.ShouldDiscard(item).ShouldBeFalse();
        }

        [Fact]
        public void CanDiscardIsTrue()
        {
            this.policy.CanDiscard().ShouldBeTrue();
        }

        [Theory]
        [InlineData(false, false, true, ItemDestination.Remove)]
        [InlineData(true, false, true, ItemDestination.Remove)]
        [InlineData(true, false, false, ItemDestination.Warm)]
        [InlineData(false, false, false, ItemDestination.Cold)]
        [InlineData(false, true, true, ItemDestination.Remove)]
        [InlineData(true, true, true, ItemDestination.Remove)]
        [InlineData(true, true, false, ItemDestination.Remove)]
        [InlineData(false, true, false, ItemDestination.Remove)]
        public void RouteHot(bool wasAccessed, bool wasRemoved, bool isExpired, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, wasRemoved, isExpired);

            this.policy.RouteHot(item).ShouldBe(expectedDestination);
        }

        [Theory]
        [InlineData(false, false, true, ItemDestination.Remove)]
        [InlineData(true, false, true, ItemDestination.Remove)]
        [InlineData(true, false, false, ItemDestination.Warm)]
        [InlineData(false, false, false, ItemDestination.Cold)]
        [InlineData(false, true, true, ItemDestination.Remove)]
        [InlineData(true, true, true, ItemDestination.Remove)]
        [InlineData(true, true, false, ItemDestination.Remove)]
        [InlineData(false, true, false, ItemDestination.Remove)]
        public void RouteWarm(bool wasAccessed, bool wasRemoved, bool isExpired, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, wasRemoved, isExpired);

            this.policy.RouteWarm(item).ShouldBe(expectedDestination);
        }

        [Theory]
        [InlineData(false, false, true, ItemDestination.Remove)]
        [InlineData(true, false, true, ItemDestination.Remove)]
        [InlineData(true, false, false, ItemDestination.Warm)]
        [InlineData(false, false, false, ItemDestination.Remove)]
        [InlineData(false, true, true, ItemDestination.Remove)]
        [InlineData(true, true, true, ItemDestination.Remove)]
        [InlineData(true, true, false, ItemDestination.Remove)]
        [InlineData(false, true, false, ItemDestination.Remove)]
        public void RouteCold(bool wasAccessed, bool wasRemoved, bool isExpired, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, wasRemoved, isExpired);

            this.policy.RouteCold(item).ShouldBe(expectedDestination);
        }

        private LongTickCountLruItem<int, int> CreateItem(bool wasAccessed, bool wasRemoved, bool isExpired)
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed = wasAccessed;
            item.WasRemoved = wasRemoved;

            if (isExpired)
            {
                item.TickCount = Duration.SinceEpoch().raw - Duration.FromSeconds(11).raw;
            }

            return item;
        }

        // backcompat: remove (methods only added for TLruLongTicksPolicy)
        [Fact]
        public void CanConvertToAndFromTicks()
        {
            var time = TimeSpan.FromSeconds(10);
            var ticks = TLruLongTicksPolicy<int, int>.ToTicks(time);
            TLruLongTicksPolicy<int, int>.FromTicks(ticks).ShouldBe(time);
        }

        // backcompat: remove (methods only added for TLruLongTicksPolicy)
        [Fact]
        public void WhenTimeLessThanEqualZeroToTicksThrows()
        {
            Action toTicks = () => { TLruLongTicksPolicy<int, int>.ToTicks(TimeSpan.Zero); };

            toTicks.ShouldThrow<ArgumentOutOfRangeException>();
        }

        // backcompat: remove (methods only added for TLruLongTicksPolicy)
        [Fact]
        public void WhenTimeGreaterThanMaxToTicksThrows()
        {
            Action toTicks = () => { TLruLongTicksPolicy<int, int>.ToTicks(TimeSpan.MaxValue); };

            toTicks.ShouldThrow<ArgumentOutOfRangeException>();
        }
    }
}

#endif
