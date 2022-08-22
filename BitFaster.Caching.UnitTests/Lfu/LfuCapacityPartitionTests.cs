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

        [Fact]
        public void WhenHitRateKeepsDecreasingWindowIsCappedAt80Percent()
        {
            int max = 100;
            var partition = new LfuCapacityPartition(max);
            var metrics = new TestMetrics();

            SetHitRate(partition, metrics, max, 0.9);

            for (int i = 0; i < 20; i++)
            {
                SetHitRate(partition, metrics, max, 0.1);
            }

            partition.Window.Should().Be(80);
            partition.Protected.Should().Be(16);
        }


        [Fact]
        public void WhenHitRateIsStableWindowConverges()
        {
            int max = 100;
            var partition = new LfuCapacityPartition(max);
            var metrics = new TestMetrics();

            // start by causing some adaptation in window so that steady state is not window = 1
            SetHitRate(partition, metrics, max, 0.9);

            for (int i = 0; i < 5; i++)
            {
                SetHitRate(partition, metrics, max, 0.1);
            }

            this.output.WriteLine("Decrease hit rate");
            SetHitRate(partition, metrics, max, 0.0);
            // window is now larger

            // go into steady state with small up and down fluctuation in hit rate
            List<int> windowSizes = new List<int>(200);
            this.output.WriteLine("Stable hit rate");

            double inc = 0.01;
            for (int i = 0; i < 200; i++)
            {
                double c = i % 2 == 0 ? inc : -inc;
                SetHitRate(partition, metrics, max, 0.9 + c);

                windowSizes.Add(partition.Window);
            }

            // verify that hit rate has converged, last 50 samples have low variance
            var last50 = windowSizes.Skip(150).Take(50).ToArray();

            var minWindow = last50.Min();
            var maxWindow = last50.Max();

            (maxWindow - minWindow).Should().BeLessThanOrEqualTo(1);
        }

        [Fact]
        public void WhenHitRateFluctuatesWindowIsAdapted()
        {
            int max = 100;
            var partition = new LfuCapacityPartition(max);
            var metrics = new TestMetrics();

            var snapshot = new WindowSnapshot();

            // steady state, window stays at 1 initially
            SetHitRate(partition, metrics, max, 0.9);
            SetHitRate(partition, metrics, max, 0.9);
            snapshot.Capture(partition);

            // Decrease hit rate, verify window increases each time
            this.output.WriteLine("1. Decrease hit rate");
            SetHitRate(partition, metrics, max, 0.1);
            snapshot.AssertWindowIncreased(partition);
            SetHitRate(partition, metrics, max, 0.1);
            snapshot.AssertWindowIncreased(partition);

            // Increase hit rate, verify window continues to increase
            this.output.WriteLine("2. Increase hit rate");
            SetHitRate(partition, metrics, max, 0.9);
            snapshot.AssertWindowIncreased(partition);

            // Decrease hit rate, verify window decreases
            this.output.WriteLine("3. Decrease hit rate");
            SetHitRate(partition, metrics, max, 0.1);
            snapshot.AssertWindowDecreased(partition);

            // Increase hit rate, verify window continues to decrease
            this.output.WriteLine("4. Increase hit rate");
            SetHitRate(partition, metrics, max, 0.9);
            snapshot.AssertWindowDecreased(partition);
            SetHitRate(partition, metrics, max, 0.9);
            snapshot.AssertWindowDecreased(partition);
        }

        private void SetHitRate(LfuCapacityPartition p, TestMetrics m, int max, double hitRate)
        {
            int total = max * 10;
            m.Hits += (long)(total * hitRate);
            m.Misses += total - (long)(total * hitRate);

            p.OptimizePartitioning(m, total);

            this.output.WriteLine($"W: {p.Window} P: {p.Protected}");
        }

        private class WindowSnapshot
        {
            private int prev;

            public void Capture(LfuCapacityPartition p)
            {
                prev = p.Window;
            }

            public void AssertWindowIncreased(LfuCapacityPartition p)
            {
                p.Window.Should().BeGreaterThan(prev);
                prev = p.Window;
            }

            public void AssertWindowDecreased(LfuCapacityPartition p)
            {
                p.Window.Should().BeLessThan(prev);
                prev = p.Window;
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
