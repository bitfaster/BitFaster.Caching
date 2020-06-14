using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class LruPolicyTests
    {
        private readonly LruPolicy<int, int> policy = new LruPolicy<int, int>();

        [Fact]
        public void CreateItemInitializesKeyAndValue()
        {
            var item = this.policy.CreateItem(1, 2);

            item.Key.Should().Be(1);
            item.Value.Should().Be(2);
        }

        [Fact]
        public void CreateItemInitializesWasAccessedFalse()
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed.Should().BeFalse();
        }

        [Fact]
        public void TouchUpdatesItemWasAccessed()
        {
            var item = this.policy.CreateItem(1, 2);
            item.WasAccessed = false;

            this.policy.Touch(item);

            item.WasAccessed.Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsNotAccessedShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);

            this.policy.ShouldDiscard(item).Should().BeFalse();
        }

        [Fact]
        public void WhenItemIsAccessedShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);
            item.WasAccessed = true;

            this.policy.ShouldDiscard(item).Should().BeFalse();
        }

        [Theory]
        [InlineData(true, ItemDestination.Warm)]
        [InlineData(false, ItemDestination.Cold)]
        public void RouteHot(bool wasAccessed, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed);

            this.policy.RouteHot(item).Should().Be(expectedDestination);
        }

        [Theory]
        [InlineData(true, ItemDestination.Warm)]
        [InlineData(false, ItemDestination.Cold)]
        public void RouteWarm(bool wasAccessed, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed);

            this.policy.RouteWarm(item).Should().Be(expectedDestination);
        }

        [Theory]
        [InlineData(true, ItemDestination.Warm)]
        [InlineData(false, ItemDestination.Remove)]
        public void RouteCold(bool wasAccessed, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed);

            this.policy.RouteCold(item).Should().Be(expectedDestination);
        }

        private LruItem<int, int> CreateItem(bool wasAccessed)
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed = wasAccessed;

            return item;
        }
    }
}
