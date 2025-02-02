﻿using FluentAssertions;
using FluentAssertions.Extensions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using BitFaster.Caching.UnitTests.Retry;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class TLruTicksPolicyTests
    {
        private readonly TLruTicksPolicy<int, int> policy = new TLruTicksPolicy<int, int>(TimeSpan.FromSeconds(10));

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

            item.TickCount.Should().BeCloseTo(Environment.TickCount, 20);
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
            item.TickCount = Environment.TickCount - (int)TimeSpan.FromSeconds(11).ToEnvTicks();

            this.policy.ShouldDiscard(item).Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsNotExpiredShouldDiscardIsFalse()
        {
            var item = this.policy.CreateItem(1, 2);
            item.TickCount = Environment.TickCount - (int)TimeSpan.FromSeconds(9).ToEnvTicks();

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

        private TickCountLruItem<int, int> CreateItem(bool wasAccessed, bool wasRemoved, bool isExpired)
        {
            var item = this.policy.CreateItem(1, 2);

            item.WasAccessed = wasAccessed;
            item.WasRemoved = wasRemoved;

            if (isExpired)
            {
                item.TickCount = Environment.TickCount - TimeSpan.FromSeconds(11).ToEnvTicks();
            }

            return item;
        }
    }

    public static class TimeSpanExtensions
    {
        public static int ToEnvTicks(this TimeSpan ts)
        {
            return (int)ts.TotalMilliseconds;
        }

        public static long ToEnvTick64(this TimeSpan ts)
        {
            return (long)ts.TotalMilliseconds;
        }
    }
}
