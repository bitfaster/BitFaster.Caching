using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class HitCounterTests
    {
        [Fact]
        public void WhenHitCountAndTotalCountAreEqualRatioIs1()
        {
            HitCounter<int, int> counter = new HitCounter<int, int>();

            counter.IncrementHit();

            counter.HitRatio.Should().Be(1.0);
        }

        [Fact]
        public void WhenHitCountIsEqualToMissCountRatioIsHalf()
        {
            HitCounter<int, int> counter = new HitCounter<int, int>();

            counter.IncrementMiss();
            counter.IncrementHit();

            counter.HitRatio.Should().Be(0.5);
        }

        [Fact]
        public void WhenTotalCountIsZeroRatioReturnsZero()
        {
            HitCounter<int, int> counter = new HitCounter<int, int>();

            counter.HitRatio.Should().Be(0.0);
        }
    }
}
