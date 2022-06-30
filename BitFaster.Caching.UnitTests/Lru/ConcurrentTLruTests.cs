using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentTLruTests
    {
        private readonly TimeSpan timeToLive = TimeSpan.FromMilliseconds(10);
        private const int capacity = 9;
        private ConcurrentTLru<int, string> lru;

        private ValueFactory valueFactory = new ValueFactory();

        private List<ItemRemovedEventArgs<int, int>> removedItems = new List<ItemRemovedEventArgs<int, int>>();

        private void OnLruItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            removedItems.Add(e);
        }

        public ConcurrentTLruTests()
        {
            lru = new ConcurrentTLru<int, string>(1, capacity, EqualityComparer<int>.Default, timeToLive);
        }

        [Fact]
        public void ConstructAddAndRetrieveWithDefaultCtorReturnsValue()
        {
            var x = new ConcurrentTLru<int, int>(3, TimeSpan.FromSeconds(1));

            x.GetOrAdd(1, k => k).Should().Be(1);
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

            await Task.Delay(timeToLive * 2);

            lru.TryGet(1, out var value).Should().BeFalse();
        }

        [Fact]
        public async Task WhenItemIsUpdatedTtlIsExtended()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            await Task.Delay(timeToLive * 2);

            lru.TryUpdate(1, "3");

            lru.TryGet(1, out var value).Should().BeTrue();
        }

        [Fact]
        public void WhenValueEvictedItemRemovedEventIsFired()
        {
            var lruEvents = new ConcurrentTLru<int, int>(1, 6, EqualityComparer<int>.Default, timeToLive);
            lruEvents.ItemRemoved += OnLruItemRemoved;

            for (int i = 0; i < 6; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => i + 1);
            }

            removedItems.Count.Should().Be(2);

            removedItems[0].Key.Should().Be(1);
            removedItems[0].Value.Should().Be(2);
            removedItems[0].Reason.Should().Be(ItemRemovedReason.Evicted);

            removedItems[1].Key.Should().Be(2);
            removedItems[1].Value.Should().Be(3);
            removedItems[1].Reason.Should().Be(ItemRemovedReason.Evicted);
        }

        [Fact]
        public void WhenItemRemovedEventIsUnregisteredEventIsNotFired()
        {
            var lruEvents = new ConcurrentTLru<int, int>(1, 6, EqualityComparer<int>.Default, timeToLive);

            lruEvents.ItemRemoved += OnLruItemRemoved;
            lruEvents.ItemRemoved -= OnLruItemRemoved;

            for (int i = 0; i < 6; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => i + 1);
            }

            removedItems.Count.Should().Be(0);
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedHitRatioIsHalf()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.HitRatio.Should().Be(0.5);
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

            await Task.Delay(timeToLive * 2);

            lru.TrimExpired();

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

            await Task.Delay(timeToLive * 2);

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            lru.TrimExpired();

            lru.Count.Should().Be(3);
        }

        [Fact]
        public async Task WhenItemsAreExpiredTrimRemovesExpiredItems()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");

            await Task.Delay(timeToLive * 2);

            lru.Trim(1);

            lru.Count.Should().Be(0);
        }
    }
}
