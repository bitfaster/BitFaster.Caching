using Shouldly;
using BitFaster.Caching.Lru;
using System;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class LruPolicyTests
    {
        private readonly LruPolicy<int, int> policy = new LruPolicy<int, int>();

        [Fact]
        public void TimeToLiveIsInfinite()
        {
            this.policy.TimeToLive.ShouldBe(new TimeSpan(0, 0, 0, 0, -1));
        }

        [Fact]
        public void CreateItemInitializesKeyAndValue()
        {
            var item = this.policy.CreateItem(1, 2);

            item.Key.ShouldBe(1);
            item.Value.ShouldBe(2);
        }

        [Fact]
        public void CreateItemInitializesWasAccessedFalse()
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed.ShouldBeFalse();
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
        public void WhenItemIsNotAccessedShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);

            this.policy.ShouldDiscard(item).ShouldBeFalse();
        }

        [Fact]
        public void WhenItemIsAccessedShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);
            item.WasAccessed = true;

            this.policy.ShouldDiscard(item).ShouldBeFalse();
        }

        [Fact]
        public void CanDiscardIsFalse()
        {
            this.policy.CanDiscard().ShouldBeFalse();
        }

        [Theory]
        [InlineData(true, ItemDestination.Warm)]
        [InlineData(false, ItemDestination.Cold)]
        public void RouteHot(bool wasAccessed, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed);

            this.policy.RouteHot(item).ShouldBe(expectedDestination);
        }

        [Theory]
        [InlineData(true, ItemDestination.Warm)]
        [InlineData(false, ItemDestination.Cold)]
        public void RouteWarm(bool wasAccessed, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed);

            this.policy.RouteWarm(item).ShouldBe(expectedDestination);
        }

        [Theory]
        [InlineData(true, ItemDestination.Warm)]
        [InlineData(false, ItemDestination.Remove)]
        public void RouteCold(bool wasAccessed, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed);

            this.policy.RouteCold(item).ShouldBe(expectedDestination);
        }

        private LruItem<int, int> CreateItem(bool wasAccessed)
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed = wasAccessed;

            return item;
        }
    }
}
