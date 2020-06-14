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
            HitCounter counter = new HitCounter();
            counter.IncrementTotalCount();
            counter.IncrementHitCount();

            counter.HitRatio.Should().Be(1.0);
        }

        [Fact]
        public void WhenHitCountIsHalfTotalCountRatioIsHalf()
        {
            HitCounter counter = new HitCounter();

            counter.IncrementTotalCount();
            counter.IncrementTotalCount();
            counter.IncrementHitCount();

            counter.HitRatio.Should().Be(0.5);
        }

        [Fact]
        public void WhenTotalCountIsZeroRatioReturnsZero()
        {
            HitCounter counter = new HitCounter();

            counter.IncrementHitCount();

            counter.HitRatio.Should().Be(0.0);
        }
    }
}
