using System;
using System.Diagnostics;
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

        private static readonly ulong stopwatchDelta = (ulong)StopwatchTickConverter.ToTicks(TimeSpan.FromMilliseconds(20));
        private static readonly ulong tickCountDelta = (ulong)TimeSpan.FromMilliseconds(20).ToEnvTick64();

        private static readonly TimeSpan defaultTimeToExpire = TimeSpan.FromSeconds(10);

        public DiscretePolicyTests() 
        {
            expiryCalculator = new TestExpiryCalculator<int, int>();
            expiryCalculator.ExpireAfterCreate = (_, _) => defaultTimeToExpire;
            expiryCalculator.ExpireAfterRead = (_, _, _) => defaultTimeToExpire;
            expiryCalculator.ExpireAfterUpdate = (_, _, _) => defaultTimeToExpire;
            policy = new DiscretePolicy<int, int>(expiryCalculator);
        }

        [Fact]
        public void TimeToLiveShouldBeZero()
        {
            this.policy.TimeToLive.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void ConvertTicksReturnsTimeSpan()
        {
#if NETFRAMEWORK
            this.policy.ConvertTicks(StopwatchTickConverter.ToTicks(defaultTimeToExpire)).Should().Be(defaultTimeToExpire);
#else
            this.policy.ConvertTicks(defaultTimeToExpire.ToEnvTick64()).Should().Be(defaultTimeToExpire);
#endif
        }

        [Fact]
        public void CreateItemInitializesKeyValueAndTicks()
        {
            var timeToExpire = TimeSpan.FromHours(1);

            expiryCalculator.ExpireAfterCreate = (k, v) => 
            {
                k.Should().Be(1);
                v.Should().Be(2);
                return timeToExpire;
            };
            
            var item = this.policy.CreateItem(1, 2);

            item.Key.Should().Be(1);
            item.Value.Should().Be(2);
#if NETFRAMEWORK
            item.TickCount.Should().BeCloseTo(StopwatchTickConverter.ToTicks(timeToExpire) + Stopwatch.GetTimestamp(), stopwatchDelta);
#else
            item.TickCount.Should().BeCloseTo((long)timeToExpire.TotalMilliseconds + Environment.TickCount64, tickCountDelta);
#endif
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

#if NETFRAMEWORK
            item.TickCount = item.TickCount - StopwatchTickConverter.ToTicks(TimeSpan.FromSeconds(11));
#else
            item.TickCount = item.TickCount - TimeSpan.FromSeconds(11).ToEnvTick64();
#endif
            this.policy.ShouldDiscard(item).Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsNotExpiredShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);

#if NETFRAMEWORK
            item.TickCount = item.TickCount - StopwatchTickConverter.ToTicks(TimeSpan.FromSeconds(9));
#else
            item.TickCount = item.TickCount - (int)TimeSpan.FromSeconds(9).ToEnvTick64();
#endif

            this.policy.ShouldDiscard(item).Should().BeFalse();
        }

        [Fact]
        public void CanDiscardIsTrue()
        {
            this.policy.CanDiscard().Should().BeTrue();
        }


        [Theory]
        [InlineData(false, true, ItemDestination.Remove)]
        [InlineData(true, true, ItemDestination.Remove)]
        [InlineData(true, false, ItemDestination.Warm)]
        [InlineData(false, false, ItemDestination.Cold)]
        public void RouteHot(bool wasAccessed, bool isExpired, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, isExpired);

            this.policy.RouteHot(item).Should().Be(expectedDestination);
        }

        [Theory]
        [InlineData(false, true, ItemDestination.Remove)]
        [InlineData(true, true, ItemDestination.Remove)]
        [InlineData(true, false, ItemDestination.Warm)]
        [InlineData(false, false, ItemDestination.Cold)]
        public void RouteWarm(bool wasAccessed, bool isExpired, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, isExpired);

            this.policy.RouteWarm(item).Should().Be(expectedDestination);
        }

        [Theory]
        [InlineData(false, true, ItemDestination.Remove)]
        [InlineData(true, true, ItemDestination.Remove)]
        [InlineData(true, false, ItemDestination.Warm)]
        [InlineData(false, false, ItemDestination.Remove)]
        public void RouteCold(bool wasAccessed, bool isExpired, ItemDestination expectedDestination)
        {
            var item = CreateItem(wasAccessed, isExpired);

            this.policy.RouteCold(item).Should().Be(expectedDestination);
        }

        private LongTickCountLruItem<int, int> CreateItem(bool wasAccessed, bool isExpired)
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed = wasAccessed;

            if (isExpired)
            {
#if NETFRAMEWORK
                item.TickCount = item.TickCount - StopwatchTickConverter.ToTicks(TimeSpan.FromSeconds(11));
#else
                item.TickCount = item.TickCount - TimeSpan.FromSeconds(11).ToEnvTick64();
#endif
            }

            return item;
        }
    }
}
