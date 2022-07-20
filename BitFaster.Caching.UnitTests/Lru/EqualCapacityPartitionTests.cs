using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class EqualCapacityPartitionTests
    {
        [Fact]
        public void WhenCapacityBelow3Throws()
        {
            Action constructor = () => { var x = new EqualCapacityPartition(2); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(3, 1, 1, 1)]
        [InlineData(4, 1, 2, 1)]
        [InlineData(5, 1, 2, 2)]
        [InlineData(6, 2, 2, 2)]
        public void EqualPartitioningCreatesEqualQueues(int totalCapacity, int expectedHot, int expectedWarm, int expectedCold)
        {
            var p = new EqualCapacityPartition(totalCapacity);

            p.Hot.Should().Be(expectedHot);
            p.Warm.Should().Be(expectedWarm);
            p.Cold.Should().Be(expectedCold);
        }
    }
}
