using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class NullHitCounterTests
    {
        private NoTelemetryPolicy<int, int> counter = new NoTelemetryPolicy<int, int>();

        [Fact]
        public void HitRatioIsZero()
        {
            counter.HitRatio.Should().Be(0);
        }

        [Fact]
        public void IncrementHitCountIsNoOp()
        {
            counter.Invoking(c => c.IncrementHit()).Should().NotThrow();
        }

        [Fact]
        public void IncrementTotalCountIsNoOp()
        {
            counter.Invoking(c => c.IncrementMiss()).Should().NotThrow();
        }
    }
}
