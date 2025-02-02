using Shouldly;
using BitFaster.Caching.Lru;
using System;
using System.Threading.Tasks;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class TLruDateTimePolicyTests
    {
        private readonly TLruDateTimePolicy<int, int> policy = new TLruDateTimePolicy<int, int>(TimeSpan.FromSeconds(10));

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

            item.TimeStamp.ShouldBe(DateTime.UtcNow, TimeSpan.FromMilliseconds(100));
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
            var ts = item.TimeStamp;

            await Task.Delay(TimeSpan.FromMilliseconds(1));

            this.policy.Update(item);

            item.TimeStamp.ShouldBeGreaterThan(ts);
        }

        [Fact]
        public void WhenItemIsExpiredShouldDiscardIsTrue()
        {
            var item = this.policy.CreateItem(1, 2);
            item.TimeStamp = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(11));

            this.policy.ShouldDiscard(item).ShouldBeTrue();
        }

        [Fact]
        public void WhenItemIsNotExpiredShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);
            item.TimeStamp = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(9));

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

        private TimeStampedLruItem<int, int> CreateItem(bool wasAccessed, bool isExpired)
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed = wasAccessed;

            if (isExpired)
            {
                item.TimeStamp = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(11));
            }

            return item;
        }
    }
}
