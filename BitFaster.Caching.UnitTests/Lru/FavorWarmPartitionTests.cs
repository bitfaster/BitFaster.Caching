﻿using System;
using BitFaster.Caching.Lru;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class FavorWarmPartitionTests
    {
        [Fact]
        public void WhenCapacityBelow3Throws()
        {
            Action constructor = () => { var x = new FavorWarmPartition(2); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenRatioBelow0Throws()
        {
            Action constructor = () => { var x = new FavorWarmPartition(5, 0.0); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenRatioAbove1Throws()
        {
            Action constructor = () => { var x = new FavorWarmPartition(5, 1.0); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(3, 1, 1, 1)]
        [InlineData(4, 1, 2, 1)]
        [InlineData(5, 1, 3, 1)]
        [InlineData(6, 1, 4, 1)]
        [InlineData(7, 1, 5, 1)]
        [InlineData(8, 1, 6, 1)]
        [InlineData(9, 1, 7, 1)]
        [InlineData(10, 1, 8, 1)]
        [InlineData(11, 1, 9, 1)]
        [InlineData(12, 1, 10, 1)]
        [InlineData(20, 2, 16, 2)]
        [InlineData(100, 10, 80, 10)]
        public void EqualPartitioningCreatesEqualQueues(int totalCapacity, int expectedHot, int expectedWarm, int expectedCold)
        {
            var p = new FavorWarmPartition(totalCapacity);

            p.Hot.ShouldBe(expectedHot);
            p.Warm.ShouldBe(expectedWarm);
            p.Cold.ShouldBe(expectedCold);
        }
    }
}
