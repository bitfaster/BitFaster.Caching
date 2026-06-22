using System.Collections.Generic;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class WeightedNodePolicyTests
    {
        private const int Capacity = 100;

        private ConcurrentLfuCore<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, NoEventPolicy<int, int>>, NoEventPolicy<int, int>> core;

        public WeightedNodePolicyTests()
        {
            core = CreateWeighted(Capacity, new ValueWeigher());
        }

        private static ConcurrentLfuCore<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, NoEventPolicy<int, int>>, NoEventPolicy<int, int>> CreateWeighted(int capacity, IWeigher<int, int> weigher)
        {
            var policy = new WeightedAccessOrderPolicy<int, int, NoEventPolicy<int, int>>(weigher);
            return new(1, capacity, new NullScheduler(), EqualityComparer<int>.Default, () => { }, policy, default);
        }

        [Fact]
        public void WhenItemsAddedWithinCapacityWeightedSizeEqualsTotalWeight()
        {
            core.AddOrUpdate(1, 10);
            core.AddOrUpdate(2, 20);
            core.AddOrUpdate(3, 30);
            core.DoMaintenance();

            core.Count.Should().Be(3);
            core.WeightedSize.Should().Be(60);
        }

        [Fact]
        public void WhenTotalWeightExceedsCapacityWeightedSizeStaysWithinMaximum()
        {
            for (int i = 0; i < 20; i++)
            {
                core.AddOrUpdate(i, 30);
            }
            core.DoMaintenance();

            core.WeightedSize.Should().BeLessThanOrEqualTo(Capacity);
        }

        [Fact]
        public void WhenItemWeightExceedsMaximumItemIsEvicted()
        {
            core.AddOrUpdate(1, 200);
            core.DoMaintenance();

            core.TryGet(1, out _).Should().BeFalse();
            core.Count.Should().Be(0);
            core.WeightedSize.Should().Be(0);
        }

        [Fact]
        public void WhenItemHasZeroWeightItIsNotEvicted()
        {
            core.AddOrUpdate(1, 0);
            core.AddOrUpdate(2, 60);
            core.AddOrUpdate(3, 60);
            core.DoMaintenance();

            core.TryGet(1, out _).Should().BeTrue();
            core.WeightedSize.Should().BeLessThanOrEqualTo(Capacity);
        }

        [Fact]
        public void WhenItemWeightIncreasedWeightedSizeIncreases()
        {
            core.AddOrUpdate(1, 30);
            core.DoMaintenance();
            core.WeightedSize.Should().Be(30);

            core.AddOrUpdate(1, 50);
            core.DoMaintenance();

            core.TryGet(1, out _).Should().BeTrue();
            core.WeightedSize.Should().Be(50);
        }

        [Fact]
        public void WhenItemWeightDecreasedWeightedSizeDecreases()
        {
            core.AddOrUpdate(1, 80);
            core.DoMaintenance();
            core.WeightedSize.Should().Be(80);

            core.AddOrUpdate(1, 10);
            core.DoMaintenance();

            core.TryGet(1, out _).Should().BeTrue();
            core.WeightedSize.Should().Be(10);
        }

        [Fact]
        public void WhenItemRemovedWeightedSizeIsDiscounted()
        {
            core.AddOrUpdate(1, 30);
            core.AddOrUpdate(2, 40);
            core.DoMaintenance();
            core.WeightedSize.Should().Be(70);

            core.TryRemove(1);
            core.DoMaintenance();

            core.WeightedSize.Should().Be(40);
            core.Count.Should().Be(1);
        }

        [Fact]
        public void WhenProbationItemReadItIsPromotedAndProtectedWeightTracked()
        {
            core.AddOrUpdate(1, 10);
            core.AddOrUpdate(2, 20);
            core.AddOrUpdate(3, 30);
            core.DoMaintenance();

            // read item 1 so it is promoted from probation to protected during maintenance
            core.TryGet(1, out _);
            core.TryGet(1, out _);
            core.DoMaintenance();

            core.TryGet(1, out _).Should().BeTrue();
            core.WeightedSize.Should().Be(60);
            core.MainProtectedWeightedSize.Should().Be(10);
        }

        private sealed class ValueWeigher : IWeigher<int, int>
        {
            public int Weigh(int key, int value) => value;
        }
    }
}
