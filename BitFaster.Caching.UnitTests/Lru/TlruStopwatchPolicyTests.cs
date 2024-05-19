using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Threading.Tasks;
using Xunit;
using System.Diagnostics;
using BitFaster.Caching.UnitTests.Retry;

namespace BitFaster.Caching.UnitTests.Lru
{
// backcompat: remove conditional compile
#if !NETCOREAPP3_1_OR_GREATER
    public class TlruStopwatchPolicyTests
    {
        private readonly TLruLongTicksPolicy<int, int> policy = new TLruLongTicksPolicy<int, int>(TimeSpan.FromSeconds(10));

        [Fact]
        public void WhenTtlIsZeroThrow()
        {
            Action constructor = () => { new TLruLongTicksPolicy<int, int>(TimeSpan.Zero); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsTimeSpanMaxThrow()
        {
            Action constructor = () => { new TLruLongTicksPolicy<int, int>(TimeSpan.MaxValue); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTtlIsCloseToMaxAllow()
        {
            var ttl = Duration.MaxRepresentable;

            new TLruLongTicksPolicy<int, int>(ttl).TimeToLive.Should().BeCloseTo(ttl, TimeSpan.FromSeconds(1));
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

            item.TickCount.Should().BeCloseTo(Duration.SinceEpoch().raw, DurationTests.epsilon);
        }

        [Fact]
        public void TouchUpdatesItemWasAccessed()
        {
            var item = this.policy.CreateItem(1, 2);
            item.WasAccessed = false;

            this.policy.Touch(item);

            item.WasAccessed.Should().BeTrue();
        }

        [RetryFact]
        public async Task UpdateUpdatesTickCount()
        {
            var item = this.policy.CreateItem(1, 2);
            var tc = item.TickCount;

            await Task.Delay(TimeSpan.FromMilliseconds(10));

            this.policy.Update(item);

            item.TickCount.Should().BeGreaterThan(tc);
        }

        [Fact]
        public void WhenItemIsExpiredShouldDiscardIsTrue()
        {
            var item = this.policy.CreateItem(1, 2);
            item.TickCount = Duration.SinceEpoch().raw - Duration.FromSeconds(11).raw;

            this.policy.ShouldDiscard(item).Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsNotExpiredShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);
            item.TickCount = Duration.SinceEpoch().raw - Duration.FromSeconds(9).raw;

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
                item.TickCount = Duration.SinceEpoch().raw - Duration.FromSeconds(11).raw;
            }

            return item;
        }
    }
#endif
}
