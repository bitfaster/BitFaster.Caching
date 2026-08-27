using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class WeightedLfuCapacityPartitionTests
    {
        [Fact]
        public void CapacityReturnsCapacity()
        {
            var partition = new WeightedLfuCapacityPartition(123);
            partition.Capacity.Should().Be(123);
        }

        [Fact]
        public void MaximumEqualsCapacity()
        {
            var partition = new WeightedLfuCapacityPartition(100);
            partition.Maximum.Should().Be(100);
        }

        [Theory]
        [InlineData(3, 1, 1)]
        [InlineData(100, 1, 79)]
        [InlineData(1000, 10, 792)]
        public void CtorSetsExpectedWeightedMaximums(int capacity, long expectedWindowMaximum, long expectedMainProtectedMaximum)
        {
            var partition = new WeightedLfuCapacityPartition(capacity);

            partition.Maximum.Should().Be(capacity);
            partition.WindowMaximum.Should().Be(expectedWindowMaximum);
            partition.MainProtectedMaximum.Should().Be(expectedMainProtectedMaximum);
        }
    }
}
