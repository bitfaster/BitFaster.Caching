using Shouldly;
using BitFaster.Caching.Lru;
using System;
using System.Threading.Tasks;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class AfterAccessPolicyTests
    {
        private readonly AfterAccessPolicy<int, int> policy = new AfterAccessPolicy<int, int>(TimeSpan.FromSeconds(10));

        [Fact]
        public void WhenTtlIsTimeSpanMaxThrow()
        {
            Action constructor = () => { new AfterAccessPolicy<int, int>(TimeSpan.MaxValue); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsZeroThrow()
        {
            Action constructor = () => { new AfterAccessPolicy<int, int>(TimeSpan.Zero); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsMaxSetAsMax()
        {
            var policy = new AfterAccessPolicy<int, int>(Duration.MaxRepresentable);
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
        public async Task TouchUpdatesTicksCount()
        {
            var item = this.policy.CreateItem(1, 2);
            var tc = item.TickCount;
            await Task.Delay(TimeSpan.FromMilliseconds(1));

            this.policy.ShouldDiscard(item); // set the time in the policy
            this.policy.Touch(item);

            item.TickCount.ShouldBeGreaterThan(tc);
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
        [InlineData(false, true, ItemDestination.Remove)]
        [InlineData(true, true, ItemDestination.Remove)]
        [InlineData(true, false, ItemDestination.Warm)]
        [InlineData(false, false, ItemDestination.Cold)]
        public void RouteHot(bool wasAccessed, bool isExpired, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, isExpired);

            this.policy.RouteHot(item).ShouldBe(expectedDestination);
        }

        [Theory]
        [InlineData(false, true, ItemDestination.Remove)]
        [InlineData(true, true, ItemDestination.Remove)]
        [InlineData(true, false, ItemDestination.Warm)]
        [InlineData(false, false, ItemDestination.Cold)]
        public void RouteWarm(bool wasAccessed, bool isExpired, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, isExpired);

            this.policy.RouteWarm(item).ShouldBe(expectedDestination);
        }

        [Theory]
        [InlineData(false, true, ItemDestination.Remove)]
        [InlineData(true, true, ItemDestination.Remove)]
        [InlineData(true, false, ItemDestination.Warm)]
        [InlineData(false, false, ItemDestination.Remove)]
        public void RouteCold(bool wasAccessed, bool isExpired, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, isExpired);

            this.policy.RouteCold(item).ShouldBe(expectedDestination);
        }

        private LongTickCountLruItem<int, int> CreateItem(bool wasAccessed, bool isExpired)
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed = wasAccessed;

            if (isExpired)
            {
                item.TickCount = Duration.SinceEpoch().raw - Duration.FromSeconds(11).raw;
            }

            return item;
        }
    }
}
