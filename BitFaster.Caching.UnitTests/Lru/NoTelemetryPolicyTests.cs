using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class NoTelemetryPolicyTests
    {
        private NoTelemetryPolicy<int, int> counter = new NoTelemetryPolicy<int, int>();

        [Fact]
        public void HitRatioIsZero()
        {
            counter.HitRatio.Should().Be(0);
        }

        [Fact]
        public void TotalIsZero()
        {
            counter.Total.Should().Be(0);
        }

        [Fact]
        public void HitsIsZero()
        {
            counter.Hits.Should().Be(0);
        }

        [Fact]
        public void MissesIsZero()
        {
            counter.Misses.Should().Be(0);
        }

        [Fact]
        public void UpdatedIsZero()
        {
            counter.Updated.Should().Be(0);
        }

        [Fact]
        public void EvictedIsZero()
        {
            counter.Evicted.Should().Be(0);
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

        [Fact]
        public void OnItemUpdatedIsNoOp()
        {
            counter.Invoking(c => c.OnItemUpdated(1, 2, 3)).Should().NotThrow();
        }

        [Fact]
        public void OnItemRemovedIsNoOp()
        {
            counter.Invoking(c => c.OnItemRemoved(1, 2, ItemRemovedReason.Evicted)).Should().NotThrow();
        }

        [Fact]
        public void RegisterRemovedEventHandlerIsNoOp()
        {
            counter.ItemRemoved += OnItemRemoved;
            counter.ItemRemoved -= OnItemRemoved;
        }

        [Fact]
        public void RegisterUpdateEventHandlerIsNoOp()
        {
            counter.ItemUpdated += OnItemUpdated;
            counter.ItemUpdated -= OnItemUpdated;
        }

        private void OnItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
        }

        private void OnItemUpdated(object sender, ItemUpdatedEventArgs<int, int> e)
        {
        }
    }
}
