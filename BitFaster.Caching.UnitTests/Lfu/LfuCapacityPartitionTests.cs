using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class LfuCapacityPartitionTests
    {
        [Fact]
        public void WhenCapacityIsLessThan3CtorThrows()
        {
            Action constructor = () => { var partition = new LfuCapacityPartition(2); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void CapacityReturnsCapacity()
        {
            var partition = new LfuCapacityPartition(123);
            partition.Capacity.Should().Be(123);
        }

        [Theory]
        [InlineData(3, 1, 1, 1)]
        [InlineData(100, 1, 79, 20)]
        public void CtorSetsExpectedCapacity(int capacity, int expectedWindow, int expectedProtected, int expectedProbation)
        {
            var partition = new LfuCapacityPartition(capacity);

            partition.Window.Should().Be(expectedWindow);
            partition.Protected.Should().Be(expectedProtected);
            partition.Probation.Should().Be(expectedProbation);
        }
    }
}
