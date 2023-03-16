using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Threading.Tasks;
using Xunit;
using System.Diagnostics;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class TlruStopwatchPolicyTests
    {
        private readonly TlruStopwatchPolicy<int, int> policy = new TlruStopwatchPolicy<int, int>(TimeSpan.FromSeconds(10));

        [Fact]
        public void WhenTtlIsZeroThrow()
        {
            Action constructor = () => { new TlruStopwatchPolicy<int, int>(TimeSpan.Zero); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsTimeSpanMaxThrow()
        {
            Action constructor = () => { new TlruStopwatchPolicy<int, int>(TimeSpan.MaxValue); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsCloseToMaxAllow()
        {
            double maxTicks = long.MaxValue / 100.0d;
            var ttl = TimeSpan.FromTicks((long)maxTicks) - TimeSpan.FromTicks(10);

            new TlruStopwatchPolicy<int, int>(ttl).TimeToLive.Should().BeCloseTo(ttl, TimeSpan.FromTicks(20));
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

            // seconds = ticks / Stopwatch.Frequency
            ulong epsilon = (ulong)(TimeSpan.FromMilliseconds(20).TotalSeconds * Stopwatch.Frequency);
            item.TickCount.Should().BeCloseTo(Stopwatch.GetTimestamp(), epsilon);
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
            item.TickCount = Stopwatch.GetTimestamp() - TlruStopwatchPolicy<int, int>.ToTicks(TimeSpan.FromSeconds(11));

            this.policy.ShouldDiscard(item).Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsNotExpiredShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);
            item.TickCount = Stopwatch.GetTimestamp() - TlruStopwatchPolicy<int, int>.ToTicks(TimeSpan.FromSeconds(9));

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
                item.TickCount = Stopwatch.GetTimestamp() - TlruStopwatchPolicy<int, int>.ToTicks(TimeSpan.FromSeconds(11));
            }

            return item;
        }
    }
}
