using System;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using Shouldly;
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

            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void TimeToLiveShouldBeZero()
        {
            this.policy.TimeToLive.ShouldBe(TimeSpan.Zero);
        }

        [Fact]
        public void CreateItemInitializesKeyValueAndTicks()
        {
            var timeToExpire = Duration.FromMinutes(60);

            expiryCalculator.ExpireAfterCreate = (k, v) => 
            {
                k.ShouldBe(1);
                v.ShouldBe(2);
                return timeToExpire;
            };
            
            var item = this.policy.CreateItem(1, 2);

            item.Key.ShouldBe(1);
            item.Value.ShouldBe(2);

            item.TickCount.ShouldBe(timeToExpire.raw + Duration.SinceEpoch().raw);
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

            await Task.Delay(TimeSpan.FromMilliseconds(20));

            this.policy.Update(item);

            item.TickCount.ShouldBeGreaterThan(tc);
        }

        [Fact]
        public void WhenItemIsExpiredShouldDiscardIsTrue()
        {
            var item = this.policy.CreateItem(1, 2);

            item.TickCount = item.TickCount - Duration.FromMilliseconds(11).raw;

            this.policy.ShouldDiscard(item).ShouldBeTrue();
        }

        [Fact]
        public void WhenItemIsNotExpiredShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);

            item.TickCount = item.TickCount - Duration.FromMilliseconds(9).raw;

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
                item.TickCount = item.TickCount - Duration.FromMilliseconds(11).raw;
            }

            return item;
        }
    }
}
