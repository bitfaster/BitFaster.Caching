using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Runtime.InteropServices;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentTLruTests
    {
        private readonly TimeSpan timeToLive = TimeSpan.FromMilliseconds(10);
        private readonly ICapacityPartition capacity = new EqualCapacityPartition(9);
        private ConcurrentTLru<int, string> lru;

        private ValueFactory valueFactory = new ValueFactory();

        private List<ItemRemovedEventArgs<int, int>> removedItems = new List<ItemRemovedEventArgs<int, int>>();

        // on MacOS time measurement seems to be less stable, give longer pause
        private int ttlWaitMlutiplier = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 8 : 2;

        private void OnLruItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            removedItems.Add(e);
        }

        public ConcurrentTLruTests()
        {
            lru = new ConcurrentTLru<int, string>(1, capacity, EqualityComparer<int>.Default, timeToLive);
        }

        [Fact]
        public void ConstructWithDefaultCtorReturnsCapacity()
        {
            var x = new ConcurrentTLru<int, int>(3, TimeSpan.FromSeconds(1));

            x.Capacity.Should().Be(3);
        }

        [Fact]
        public void ConstructCapacityCtorReturnsCapacity()
        {
            var x = new ConcurrentTLru<int, int>(1, 3, EqualityComparer<int>.Default, TimeSpan.FromSeconds(1));

            x.Capacity.Should().Be(3);
        }

        [Fact]
        public void ConstructPartitionCtorReturnsCapacity()
        {
            var x = new ConcurrentTLru<int, int>(1, new EqualCapacityPartition(3), EqualityComparer<int>.Default, TimeSpan.FromSeconds(1));

            x.Capacity.Should().Be(3);
        }

        [Fact]
        public void CanExpireIsTrue()
        {
            this.lru.Policy.ExpireAfterWrite.HasValue.Should().BeTrue();
        }

        [Fact]
        public void TimeToLiveIsCtorArg()
        {
            this.lru.Policy.ExpireAfterWrite.Value.TimeToLive.Should().Be(timeToLive);
        }

        [Fact]
        public void WhenItemIsNotExpiredItIsNotRemoved()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryGet(1, out var value).Should().BeTrue();
        }

        [Fact]
        public async Task WhenItemIsExpiredItIsRemoved()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            await Task.Delay(timeToLive * ttlWaitMlutiplier);

            lru.TryGet(1, out var value).Should().BeFalse();
        }

        [Fact]
        public async Task WhenItemIsUpdatedTtlIsExtended()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            await Task.Delay(timeToLive * ttlWaitMlutiplier);

            lru.TryUpdate(1, "3");

            lru.TryGet(1, out var value).Should().BeTrue();
        }

        [Fact]
        public void WhenValueEvictedItemRemovedEventIsFired()
        {
            var lruEvents = new ConcurrentTLru<int, int>(1, new EqualCapacityPartition(6), EqualityComparer<int>.Default, timeToLive);
            lruEvents.Events.Value.ItemRemoved += OnLruItemRemoved;

            // First 6 adds
            // hot[6, 5], warm[2, 1], cold[4, 3]
            // =>
            // hot[8, 7], warm[1, 0], cold[6, 5], evicted[4, 3]
            for (int i = 0; i < 8; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => i + 1);
            }

            removedItems.Count.Should().Be(2);

            removedItems[0].Key.Should().Be(3);
            removedItems[0].Value.Should().Be(4);
            removedItems[0].Reason.Should().Be(ItemRemovedReason.Evicted);

            removedItems[1].Key.Should().Be(4);
            removedItems[1].Value.Should().Be(5);
            removedItems[1].Reason.Should().Be(ItemRemovedReason.Evicted);
        }

        [Fact]
        public void WhenItemRemovedEventIsUnregisteredEventIsNotFired()
        {
            var lruEvents = new ConcurrentTLru<int, int>(1, new EqualCapacityPartition(6), EqualityComparer<int>.Default, timeToLive);
            lruEvents.Events.Value.ItemRemoved += OnLruItemRemoved;
            lruEvents.Events.Value.ItemRemoved -= OnLruItemRemoved;

            for (int i = 0; i < 6; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => i + 1);
            }

            removedItems.Count.Should().Be(0);
        }

        [Fact]
        public async Task WhenItemsAreExpiredExpireRemovesExpiredItems()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            lru.AddOrUpdate(4, "4");
            lru.AddOrUpdate(5, "5");
            lru.AddOrUpdate(6, "6");

            lru.AddOrUpdate(7, "7");
            lru.AddOrUpdate(8, "8");
            lru.AddOrUpdate(9, "9");

            await Task.Delay(timeToLive * ttlWaitMlutiplier);

            lru.Policy.ExpireAfterWrite.Value.TrimExpired();

            lru.Count.Should().Be(0);
        }

        [Fact]
        public async Task WhenCacheHasExpiredAndFreshItemsExpireRemovesOnlyExpiredItems()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");

            lru.AddOrUpdate(4, "4");
            lru.AddOrUpdate(5, "5");
            lru.AddOrUpdate(6, "6");

            await Task.Delay(timeToLive * ttlWaitMlutiplier);

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            lru.Policy.ExpireAfterWrite.Value.TrimExpired();

            lru.Count.Should().Be(3);
        }

        [Fact]
        public async Task WhenItemsAreExpiredTrimRemovesExpiredItems()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");

            await Task.Delay(timeToLive * ttlWaitMlutiplier);

            lru.Trim(1);

            lru.Count.Should().Be(0);
        }
    }
}
