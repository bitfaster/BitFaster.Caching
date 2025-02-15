using Shouldly;
using BitFaster.Caching.Lru;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class NoTelemetryPolicyTests
    {
        private NoTelemetryPolicy<int, int> counter = new NoTelemetryPolicy<int, int>();

        [Fact]
        public void HitRatioIsZero()
        {
            counter.HitRatio.ShouldBe(0);
        }

        [Fact]
        public void TotalIsZero()
        {
            counter.Total.ShouldBe(0);
        }

        [Fact]
        public void HitsIsZero()
        {
            counter.Hits.ShouldBe(0);
        }

        [Fact]
        public void MissesIsZero()
        {
            counter.Misses.ShouldBe(0);
        }

        [Fact]
        public void UpdatedIsZero()
        {
            counter.Updated.ShouldBe(0);
        }

        [Fact]
        public void EvictedIsZero()
        {
            counter.Evicted.ShouldBe(0);
        }

        [Fact]
        public void IncrementHitCountIsNoOp()
        {
            Should.NotThrow(() => counter.IncrementHit());
        }

        [Fact]
        public void IncrementTotalCountIsNoOp()
        {
            Should.NotThrow(() => counter.IncrementMiss());
        }

        [Fact]
        public void OnItemUpdatedIsNoOp()
        {
            Should.NotThrow(() => counter.OnItemUpdated(1, 2, 3));
        }

        [Fact]
        public void OnItemRemovedIsNoOp()
        {
            Should.NotThrow(() => counter.OnItemRemoved(1, 2, ItemRemovedReason.Evicted));
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
