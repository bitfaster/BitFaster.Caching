using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class LfuCapacityPartitionTests
    {
        private readonly ITestOutputHelper output;

        public LfuCapacityPartitionTests(ITestOutputHelper output)
        {
            this.output = output;
        }

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

        // Objective: calculate partitions based on hit rate changes. Assume ConcurrentLru will evict things
        // scenario
        // 1. start out by always trying to increase window size in iteration 1
        // 2. if hit rate increases in iteration 2, increase hit window again
        // 3. if hit rate decreases in teration 2, decrease window
        // 4. if hit rate continues to increase, apply decay until stable
        [Fact]
        public void TestOptimize()
        {
            int max = 100;
            var partition = new LfuCapacityPartition(max);
            var metrics = new TestMetrics();

            for (int i = 0; i < 10; i++)
            {
                metrics.Hits += 1000;
                metrics.Misses += 2000;

                partition.OptimizePartitioning(metrics, 10 * max);

                this.output.WriteLine($"W: {partition.Window} P: {partition.Protected}");

            }

            this.output.WriteLine("Decrease hit rate");

            for (int i = 0; i < 2; i++)
            {
                metrics.Hits += 0001;
                metrics.Misses += 1000;

                partition.OptimizePartitioning(metrics, 10 * max);

                this.output.WriteLine($"W: {partition.Window} P: {partition.Protected}");

            }

            this.output.WriteLine("Increase hit rate");

            for (int i = 0; i < 1; i++)
            {
                metrics.Hits += 1000;
                metrics.Misses += 2000;

                partition.OptimizePartitioning(metrics, 10 * max);

                this.output.WriteLine($"W: {partition.Window} P: {partition.Protected}");

            }

            this.output.WriteLine("Decrease hit rate");

            for (int i = 0; i < 1; i++)
            {
                metrics.Hits += 0001;
                metrics.Misses += 1000;

                partition.OptimizePartitioning(metrics, 10 * max);

                this.output.WriteLine($"W: {partition.Window} P: {partition.Protected}");

            }

            this.output.WriteLine("Increase hit rate");

            for (int i = 0; i < 5; i++)
            {
                metrics.Hits += 1000;
                metrics.Misses += 2000;

                partition.OptimizePartitioning(metrics, 10 * max);

                this.output.WriteLine($"W: {partition.Window} P: {partition.Protected}");

            }
        }

        private class TestMetrics : ICacheMetrics
        {
            public double HitRatio => (double)Hits / (double)Total;

            public long Total => Hits + Misses;

            public long Hits { get; set; }

            public long Misses { get; set; }

            public long Evicted { get; set; }

            public long Updated { get; set; }
        }
    }
}
