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
        [InlineData(true, false, ItemDestination.Warm)]
        [InlineData(false, false, ItemDestination.Cold)]
        [InlineData(false, true, ItemDestination.Remove)]
        [InlineData(true, true, ItemDestination.Remove)]
        public void RouteHot(bool wasAccessed, bool wasRemoved, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, wasRemoved);

            this.policy.RouteHot(item).ShouldBe(expectedDestination);
        }

        [Theory]
        [InlineData(true, false, ItemDestination.Warm)]
        [InlineData(false, false, ItemDestination.Cold)]
        [InlineData(true, true, ItemDestination.Remove)]
        [InlineData(false, true, ItemDestination.Remove)]
        public void RouteWarm(bool wasAccessed, bool wasRemoved, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, wasRemoved);

            this.policy.RouteWarm(item).ShouldBe(expectedDestination);
        }

        [Theory]
        [InlineData(true, false, ItemDestination.Warm)]
        [InlineData(false, false, ItemDestination.Remove)]
        [InlineData(true, true, ItemDestination.Remove)]
        [InlineData(false, true, ItemDestination.Remove)]
        public void RouteCold(bool wasAccessed, bool wasRemoved, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, wasRemoved);

            this.policy.RouteCold(item).ShouldBe(expectedDestination);
        }

        private LruItem<int, int> CreateItem(bool wasAccessed, bool wasRemoved)
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed = wasAccessed;
            item.WasRemoved = wasRemoved;

            return item;
        }
    }
}
