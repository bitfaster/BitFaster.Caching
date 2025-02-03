using System;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class DiscretePolicyTests
    {
        private readonly TestExpiryCalculator<int, int> expiryCalculator;
        private readonly DiscretePolicy<int, int> policy;

        public DiscretePolicyTests() 
        {
            expiryCalculator = new TestExpiryCalculator<int, int>();
            policy = new DiscretePolicy<int, int>(expiryCalculator);
        }

        [Fact]
        public void WhenCalculatorNullThrows()
        {
            Action act = () => new DiscretePolicy<int, int>(null);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void TimeToLiveShouldBeZero()
        {
            this.policy.TimeToLive.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void CreateItemInitializesKeyValueAndTicks()
        {
            var timeToExpire = Duration.FromMinutes(60);

            expiryCalculator.ExpireAfterCreate = (k, v) => 
            {
                k.Should().Be(1);
                v.Should().Be(2);
                return timeToExpire;
            };
            
            var item = this.policy.CreateItem(1, 2);

            item.Key.Should().Be(1);
            item.Value.Should().Be(2);

            item.TickCount.Should().BeCloseTo(timeToExpire.raw + Duration.SinceEpoch().raw, DurationTests.epsilon);
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
        public async Task TouchUpdatesTicksCount()
        {
            var item = this.policy.CreateItem(1, 2);
            var tc = item.TickCount;
            await Task.Delay(TimeSpan.FromMilliseconds(1));

            this.policy.ShouldDiscard(item); // set the time in the policy
            this.policy.Touch(item);

            item.TickCount.Should().BeGreaterThan(tc);
        }

        [Fact]
        public async Task UpdateUpdatesTickCount()
        {
            var item = this.policy.CreateItem(1, 2);
            var tc = item.TickCount;

            await Task.Delay(TimeSpan.FromMilliseconds(20));

            this.policy.Update(item);

            item.TickCount.Should().BeGreaterThan(tc);
        }

        [Fact]
        public void WhenItemIsExpiredShouldDiscardIsTrue()
        {
            var item = this.policy.CreateItem(1, 2);

            item.TickCount = item.TickCount - Duration.FromMilliseconds(11).raw;

            this.policy.ShouldDiscard(item).Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsNotExpiredShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);

            item.TickCount = item.TickCount - Duration.FromMilliseconds(9).raw;

            this.policy.ShouldDiscard(item).Should().BeFalse();
        }

        [Fact]
        public void CanDiscardIsTrue()
        {
            this.policy.CanDiscard().Should().BeTrue();
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

            this.policy.RouteHot(item).Should().Be(expectedDestination);
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

            this.policy.RouteWarm(item).Should().Be(expectedDestination);
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

            this.policy.RouteCold(item).Should().Be(expectedDestination);
        }

        private LongTickCountLruItem<int, int> CreateItem(bool wasAccessed, bool wasRemoved, bool isExpired)
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed = wasAccessed;
            item.WasRemoved = wasRemoved;

            if (isExpired)
            {
                item.TickCount = item.TickCount - Duration.FromMilliseconds(11).raw;
            }

            return item;
        }
    }
}
