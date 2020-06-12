using FluentAssertions;
using Lightweight.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Lightweight.Caching.UnitTests.Lru
{
    public class NullHitCounterTests
    {
        private NullHitCounter counter = new NullHitCounter();

        [Fact]
        public void HitRatioIsZero()
        {
            counter.HitRatio.Should().Be(0);
        }

        [Fact]
        public void IncrementHitCountIsNoOp()
        {
            counter.Invoking(c => c.IncrementHitCount()).Should().NotThrow();
        }

        [Fact]
        public void IncrementTotalCountIsNoOp()
        {
            counter.Invoking(c => c.IncrementTotalCount()).Should().NotThrow();
        }
    }
}
