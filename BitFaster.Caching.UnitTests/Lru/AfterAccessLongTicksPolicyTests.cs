using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Threading.Tasks;
using Xunit;

#if NETFRAMEWORK
using System.Diagnostics;
#endif

namespace BitFaster.Caching.UnitTests.Lru
{
    public class AfterAccessLongTicksPolicyTests
    {
        private readonly AfterAccessLongTicksPolicy<int, int> policy = new AfterAccessLongTicksPolicy<int, int>(TimeSpan.FromSeconds(10));

        [Fact]
        public void WhenTtlIsTimeSpanMaxThrow()
        {
            Action constructor = () => { new AfterAccessLongTicksPolicy<int, int>(TimeSpan.MaxValue); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsZeroThrow()
        {
            Action constructor = () => { new AfterAccessLongTicksPolicy<int, int>(TimeSpan.Zero); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsMaxSetAsMax()
        {
#if NETFRAMEWORK
            var maxRepresentable = TimeSpan.FromTicks((long)(long.MaxValue / 100.0d)) - TimeSpan.FromTicks(10);
#else
            var maxRepresentable = Time.MaxRepresentable;
#endif
            var policy = new AfterAccessLongTicksPolicy<int, int>(maxRepresentable);
            policy.TimeToLive.Should().BeCloseTo(maxRepresentable, TimeSpan.FromTicks(20));
        }

        [Fact]
        public void TimeToLiveShouldBeTenSecs()
        {
            this.policy.TimeToLive.Should().Be(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void CreateItemInitializesKeyAndValue()
        {
            var item = this.policy.CreateItem(1, 2);

            item.Key.Should().Be(1);
            item.Value.Should().Be(2);
        }

        [Fact]
        public void CreateItemInitializesTimestampToNow()
        {
            var item = this.policy.CreateItem(1, 2);

#if NETFRAMEWORK
            var expected = Stopwatch.GetTimestamp();
            ulong epsilon = (ulong)(TimeSpan.FromMilliseconds(20).TotalSeconds * Stopwatch.Frequency);
#else
            var expected = Environment.TickCount64;
            ulong epsilon = 20;
#endif
            item.TickCount.Should().BeCloseTo(expected, epsilon);
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
        public void TouchUpdatesTicks()
        {
            var item = this.policy.CreateItem(1, 2);

            var lastTicks = item.TickCount;
          
            this.policy.ShouldDiscard(item); // set the time
            this.policy.Touch(item);

            item.TickCount.Should().BeGreaterThan(lastTicks);
        }

        [Fact]
        public async Task UpdateUpdatesTickCount()
        {
            var item = this.policy.CreateItem(1, 2);
            var tc = item.TickCount;

            await Task.Delay(TimeSpan.FromMilliseconds(1));

            this.policy.Update(item);

            item.TickCount.Should().BeGreaterThan(tc);
        }

        [Fact]
        public void WhenItemIsExpiredShouldDiscardIsTrue()
        {
            var item = this.policy.CreateItem(1, 2);
            item.TickCount = Environment.TickCount - (int)TimeSpan.FromSeconds(11).ToEnvTick64();

            this.policy.ShouldDiscard(item).Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsNotExpiredShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);

#if NETFRAMEWORK
            item.TickCount = Stopwatch.GetTimestamp() - StopwatchTickConverter.ToTicks(TimeSpan.FromSeconds(9));
#else
            item.TickCount = Environment.TickCount - (int)TimeSpan.FromSeconds(9).ToEnvTick64();
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
                item.TickCount = Stopwatch.GetTimestamp() - StopwatchTickConverter.ToTicks(TimeSpan.FromSeconds(11));
#else
                item.TickCount = Environment.TickCount - TimeSpan.FromSeconds(11).ToEnvTick64();
#endif
            }

            return item;
        }
    }
}
