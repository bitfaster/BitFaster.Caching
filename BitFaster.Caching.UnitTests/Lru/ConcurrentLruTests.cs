using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Collections;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentLruTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private const int hotCap = 3;
        private const int warmCap = 3;
        private const int coldCap = 3;
        private static readonly ICapacityPartition capacity = new EqualCapacityPartition(hotCap + warmCap + coldCap);

        private ConcurrentLru<int, string> lru = new ConcurrentLru<int, string>(1, capacity, EqualityComparer<int>.Default);
        private ValueFactory valueFactory = new ValueFactory();

        private List<ItemRemovedEventArgs<int, int>> removedItems = new List<ItemRemovedEventArgs<int, int>>();

        private void OnLruItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            removedItems.Add(e);
        }

        public ConcurrentLruTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void WhenConcurrencyIsLessThan1CtorThrows()
        {
            Action constructor = () => { var x = new ConcurrentLru<int, string>(0, 3, EqualityComparer<int>.Default); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenCapacityIsLessThan3CtorThrows()
        {
            Action constructor = () => { var x = new ConcurrentLru<int, string>(1, 2, EqualityComparer<int>.Default); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenPartitionIsNullCtorThrows()
        {
            ICapacityPartition partition = null;
            Action constructor = () => { var x = new ConcurrentLru<int, string>(1, partition, EqualityComparer<int>.Default); };

            constructor.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenComparerIsNullCtorThrows()
        {
            Action constructor = () => { var x = new ConcurrentLru<int, string>(1, 3, null); };

            constructor.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenCapacityIs4HotHasCapacity1AndColdHasCapacity1()
        {
            var lru = new ConcurrentLru<int, int>(1, new EqualCapacityPartition(4), EqualityComparer<int>.Default);

            for (int i = 0; i < 5; i++)
            {
                lru.GetOrAdd(i, x => x);
            }

            lru.HotCount.Should().Be(1);
            lru.ColdCount.Should().Be(1);
            lru.Capacity.Should().Be(4);
        }

        [Fact]
        public void WhenCapacityIs5HotHasCapacity1AndColdHasCapacity2()
        {
            var lru = new ConcurrentLru<int, int>(1, new EqualCapacityPartition(5), EqualityComparer<int>.Default);

            for (int i = 0; i < 5; i++)
            {
                lru.GetOrAdd(i, x => x);
            }

            lru.HotCount.Should().Be(1);
            lru.ColdCount.Should().Be(2);
            lru.Capacity.Should().Be(5);
        }

        [Fact]
        public void ConstructAddAndRetrieveWithDefaultCtorReturnsValue()
        {
            var x = new ConcurrentLru<int, int>(3);

            x.GetOrAdd(1, k => k).Should().Be(1);
        }

        [Fact]
        public void WhenItemIsAddedCountIsCorrect()
        {
            lru.Count.Should().Be(0);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.Count.Should().Be(1);
        }

        [Fact]
        public async Task WhenItemIsAddedCountIsCorrectAsync()
        {
            lru.Count.Should().Be(0);
            await lru.GetOrAddAsync(0, valueFactory.CreateAsync).ConfigureAwait(false);
            lru.Count.Should().Be(1);
        }

        [Fact]
        public void WhenItemsAddedKeysContainsTheKeys()
        {
            lru.Count.Should().Be(0);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.Keys.Should().BeEquivalentTo(new[] { 1, 2 });
        }

        [Fact]
        public void WhenItemsAddedGenericEnumerateContainsKvps()
        {
            lru.Count.Should().Be(0);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.Should().BeEquivalentTo(new[] { new KeyValuePair<int, string>(1, "1"), new KeyValuePair<int, string>(2, "2") });
        }

        [Fact]
        public void WhenItemsAddedEnumerateContainsKvps()
        {
            lru.Count.Should().Be(0);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);

            var enumerable = (IEnumerable)lru;
            enumerable.Should().BeEquivalentTo(new[] { new KeyValuePair<int, string>(1, "1"), new KeyValuePair<int, string>(2, "2") });
        }

        [Fact]
        public void FromColdWarmupFillsWarmQueue()
        {
            this.Warmup();

            this.lru.Count.Should().Be(9);
        }

        [Fact]
        public void WhenItemExistsTryGetReturnsValueAndTrue()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            result.Should().Be(true);
            value.Should().Be("1");
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetReturnsNullAndFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(2, out var value);

            result.Should().Be(false);
            value.Should().BeNull();
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedHitRatioIsHalf()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

#pragma warning disable CS0618 // Type or member is obsolete
            lru.HitRatio.Should().Be(0.5);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedMetricHitRatioIsHalf()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.Metrics.HitRatio.Should().Be(0.5);
        }

        [Fact]
        public void WhenKeyIsRequestedItIsCreatedAndCached()
        {
            var result1 = lru.GetOrAdd(1, valueFactory.Create);
            var result2 = lru.GetOrAdd(1, valueFactory.Create);

            valueFactory.timesCalled.Should().Be(1);
            result1.Should().Be(result2);
        }

        [Fact]
        public async Task WhenKeyIsRequesteItIsCreatedAndCachedAsync()
        {
            var result1 = await lru.GetOrAddAsync(1, valueFactory.CreateAsync).ConfigureAwait(false);
            var result2 = await lru.GetOrAddAsync(1, valueFactory.CreateAsync).ConfigureAwait(false);

            valueFactory.timesCalled.Should().Be(1);
            result1.Should().Be(result2);
        }

        [Fact]
        public void WhenDifferentKeysAreRequestedValueIsCreatedForEach()
        {
            var result1 = lru.GetOrAdd(1, valueFactory.Create);
            var result2 = lru.GetOrAdd(2, valueFactory.Create);

            valueFactory.timesCalled.Should().Be(2);

            result1.Should().Be("1");
            result2.Should().Be("2");
        }

        [Fact]
        public async Task WhenDifferentKeysAreRequesteValueIsCreatedForEachAsync()
        {
            var result1 = await lru.GetOrAddAsync(1, valueFactory.CreateAsync).ConfigureAwait(false);
            var result2 = await lru.GetOrAddAsync(2, valueFactory.CreateAsync).ConfigureAwait(false);

            valueFactory.timesCalled.Should().Be(2);

            result1.Should().Be("1");
            result2.Should().Be("2");
        }

        [Fact]
        public void WhenValuesAreNotReadAndMoreKeysRequestedThanCapacityCountDoesNotIncrease()
        {
            this.Warmup();

            var result = lru.GetOrAdd(1, valueFactory.Create);

            lru.Count.Should().Be(9);
            valueFactory.timesCalled.Should().Be(10);
        }

        [Fact]
        public void WhenValuesAreReadAndMoreKeysRequestedThanCapacityCountIsBounded()
        {
            int capacity = hotCap + coldCap + warmCap;
            for (int i = 0; i < capacity + 1; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);

                // touch items already cached when they are still in hot
                if (i > 0)
                {
                    lru.GetOrAdd(i - 1, valueFactory.Create);
                }
            }

            lru.Count.Should().Be(capacity);
            valueFactory.timesCalled.Should().Be(capacity + 1);
        }

        [Fact]
        public void WhenKeysAreContinuouslyRequestedInTheOrderTheyAreAddedCountIsBounded()
        {
            int capacity = hotCap + coldCap + warmCap;
            for (int i = 0; i < capacity + 10; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);

                // Touch all items already cached in hot, warm and cold.
                // This is worst case scenario, since we touch them in the exact order they
                // were added.
                for (int j = 0; j < i; j++)
                {
                    lru.GetOrAdd(j, valueFactory.Create);
                }

                testOutputHelper.WriteLine($"Total: {lru.Count} Hot: {lru.HotCount} Warm: {lru.WarmCount} Cold: {lru.ColdCount}");
                lru.Count.Should().BeLessOrEqualTo(capacity + 1);
            }
        }

        [Fact]
        public void WhenValueIsNotTouchedAndExpiresFromHotValueIsBumpedToCold()
        {
            this.Warmup();

            lru.GetOrAdd(0, valueFactory.Create); // Don't touch in hot

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);
            lru.GetOrAdd(4, valueFactory.Create);
            lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create);
            lru.GetOrAdd(7, valueFactory.Create);
            lru.GetOrAdd(8, valueFactory.Create);
            lru.GetOrAdd(9, valueFactory.Create);

            lru.TryGet(0, out var value).Should().BeFalse();
        }

        [Fact]
        public void WhenValueIsTouchedAndExpiresFromHotValueIsBumpedToWarm()
        {
            this.Warmup();

            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(0, valueFactory.Create); // Touch in hot

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);
            lru.GetOrAdd(4, valueFactory.Create);
            lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create);
            lru.GetOrAdd(7, valueFactory.Create);
            lru.GetOrAdd(8, valueFactory.Create);
            lru.GetOrAdd(9, valueFactory.Create);

            lru.TryGet(0, out var value).Should().BeTrue();
        }

        [Fact]
        public void WhenValueIsTouchedAndExpiresFromColdItIsBumpedToWarm()
        {
            this.Warmup();

            lru.GetOrAdd(0, valueFactory.Create);

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create); // push 0 to cold (not touched in hot)

            lru.GetOrAdd(0, valueFactory.Create); // Touch 0 in cold

            lru.GetOrAdd(4, valueFactory.Create); // fully cycle cold, this will evict 0 if it is not moved to warm
            lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create);
            lru.GetOrAdd(7, valueFactory.Create);
            lru.GetOrAdd(8, valueFactory.Create);
            lru.GetOrAdd(9, valueFactory.Create);

            lru.TryGet(0, out var value).Should().BeTrue();
        }

        [Fact]
        public void WhenValueIsNotTouchedAndExpiresFromColdItIsRemoved()
        {
            this.Warmup();

            lru.GetOrAdd(0, valueFactory.Create);

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create); // push 0 to cold (not touched in hot)

            // Don't touch 0 in cold

            lru.GetOrAdd(4, valueFactory.Create); // fully cycle cold, this will evict 0 if it is not moved to warm
            lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create);
            lru.GetOrAdd(7, valueFactory.Create);
            lru.GetOrAdd(8, valueFactory.Create);
            lru.GetOrAdd(9, valueFactory.Create);

            lru.TryGet(0, out var value).Should().BeFalse();
        }

        [Fact]
        public void WhenValueIsNotTouchedAndExpiresFromWarmValueIsBumpedToCold()
        {
            this.Warmup();

            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(0, valueFactory.Create); // Touch 0 in hot, it will promote to warm

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create); // push 0 to warm

            // touch next 3 values, so they will promote to warm
            lru.GetOrAdd(4, valueFactory.Create); lru.GetOrAdd(4, valueFactory.Create);
            lru.GetOrAdd(5, valueFactory.Create); lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create); lru.GetOrAdd(6, valueFactory.Create);

            // push 4,5,6 to warm, 0 to cold
            lru.GetOrAdd(7, valueFactory.Create);
            lru.GetOrAdd(8, valueFactory.Create);
            lru.GetOrAdd(9, valueFactory.Create);

            // verify 0 is present, but don't touch it
            lru.Keys.Should().Contain(0);

            // push 7,8,9 to cold, evict 0
            lru.GetOrAdd(10, valueFactory.Create);
            lru.GetOrAdd(11, valueFactory.Create);
            lru.GetOrAdd(12, valueFactory.Create);

            lru.TryGet(0, out var value).Should().BeFalse();
        }

        [Fact]
        public void WhenValueIsTouchedAndExpiresFromWarmValueIsBumpedBackIntoWarm()
        {
            this.Warmup();

            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(0, valueFactory.Create); // Touch 0 in hot, it will promote to warm

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create); // push 0 to warm

            // touch next 3 values, so they will promote to warm
            lru.GetOrAdd(4, valueFactory.Create); lru.GetOrAdd(4, valueFactory.Create);
            lru.GetOrAdd(5, valueFactory.Create); lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create); lru.GetOrAdd(6, valueFactory.Create);

            // push 4,5,6 to warm, 0 to cold
            lru.GetOrAdd(7, valueFactory.Create);
            lru.GetOrAdd(8, valueFactory.Create);
            lru.GetOrAdd(9, valueFactory.Create);

            // Touch 0
            lru.TryGet(0, out var value).Should().BeTrue();

            // push 7,8,9 to cold, cycle 0 back to warm
            lru.GetOrAdd(10, valueFactory.Create);
            lru.GetOrAdd(11, valueFactory.Create);
            lru.GetOrAdd(12, valueFactory.Create);

            lru.TryGet(0, out value).Should().BeTrue();
        }

        [Fact]
        public void WhenValueExpiresItIsDisposed()
        {
            var lruOfDisposable = new ConcurrentLru<int, DisposableItem>(1, new EqualCapacityPartition(6), EqualityComparer<int>.Default);
            var disposableValueFactory = new DisposableValueFactory();

            for (int i = 0; i < 7; i++)
            {
                lruOfDisposable.GetOrAdd(i, disposableValueFactory.Create);
            }

            disposableValueFactory.Items[0].IsDisposed.Should().BeFalse();
            disposableValueFactory.Items[1].IsDisposed.Should().BeFalse();

            disposableValueFactory.Items[2].IsDisposed.Should().BeTrue();

            disposableValueFactory.Items[3].IsDisposed.Should().BeFalse();
            disposableValueFactory.Items[4].IsDisposed.Should().BeFalse();
            disposableValueFactory.Items[5].IsDisposed.Should().BeFalse();
            disposableValueFactory.Items[6].IsDisposed.Should().BeFalse();
        }

        [Fact]
        public void WhenValueEvictedItemRemovedEventIsFired()
        {
            var lruEvents = new ConcurrentLru<int, int>(1, new EqualCapacityPartition(6), EqualityComparer<int>.Default);
            lruEvents.ItemRemoved += OnLruItemRemoved;

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
        public void WhenValuesAreEvictedEvictionMetricCountsEvicted()
        {
            this.Warmup();

            this.lru.GetOrAdd(1, valueFactory.Create);

            this.lru.Metrics.Evicted.Should().Be(1);
        }

        [Fact]
        public void WhenItemRemovedEventIsUnregisteredEventIsNotFired()
        {
            var lruEvents = new ConcurrentLru<int, int>(1, 6, EqualityComparer<int>.Default);

            lruEvents.ItemRemoved += OnLruItemRemoved;
            lruEvents.ItemRemoved -= OnLruItemRemoved;

            for (int i = 0; i < 6; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => i + 1);
            }

            removedItems.Count.Should().Be(0);
        }

        [Fact]
        public void WhenKeyExistsTryRemoveRemovesItemAndReturnsTrue()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryRemove(1).Should().BeTrue();
            lru.TryGet(1, out var value).Should().BeFalse();
        }

        [Fact]
        public void WhenItemIsRemovedItIsDisposed()
        {
            var lruOfDisposable = new ConcurrentLru<int, DisposableItem>(1, 6, EqualityComparer<int>.Default);
            var disposableValueFactory = new DisposableValueFactory();

            lruOfDisposable.GetOrAdd(1, disposableValueFactory.Create);
            lruOfDisposable.TryRemove(1);

            disposableValueFactory.Items[1].IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsRemovedRemovedEventIsFired()
        {
            var lruEvents = new ConcurrentLru<int, int>(1, 6, EqualityComparer<int>.Default);
            lruEvents.ItemRemoved += OnLruItemRemoved;

            lruEvents.GetOrAdd(1, i => i + 2);

            lruEvents.TryRemove(1).Should().BeTrue();

            removedItems.Count().Should().Be(1);
            removedItems[0].Key.Should().Be(1);
            removedItems[0].Value.Should().Be(3);
            removedItems[0].Reason.Should().Be(ItemRemovedReason.Removed);
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveReturnsFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryRemove(2).Should().BeFalse();
        }

        [Fact]
        public void WhenRepeatedlyAddingAndRemovingSameValueLruRemainsInConsistentState()
        {
            int capacity = hotCap + coldCap + warmCap;
            for (int i = 0; i < capacity; i++)
            {
                // Because TryRemove leaves the item in the queue, when it is eventually removed
                // from the cold queue, it should not remove the newly created value.
                lru.GetOrAdd(1, valueFactory.Create);
                lru.TryGet(1, out var value).Should().BeTrue();
                lru.TryRemove(1);
            }
        }

        [Fact]
        public void WhenKeyExistsTryUpdateUpdatesValueAndReturnsTrue()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryUpdate(1, "2").Should().BeTrue();

            lru.TryGet(1, out var value);
            value.Should().Be("2");
        }

        [Fact]
        public void WhenKeyExistsTryUpdateDisposesOldValue()
        {
            var lruOfDisposable = new ConcurrentLru<int, DisposableItem>(1, 6, EqualityComparer<int>.Default);
            var disposableValueFactory = new DisposableValueFactory();
            var newValue = new DisposableItem();

            lruOfDisposable.GetOrAdd(1, disposableValueFactory.Create);
            lruOfDisposable.TryUpdate(1, newValue);

            disposableValueFactory.Items[1].IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryUpdateReturnsFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryUpdate(2, "3").Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateAddsNewItem()
        {
            lru.AddOrUpdate(1, "1");

            lru.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be("1");
        }

        [Fact]
        public void WhenKeyExistsAddOrUpdateUpdatesExistingItem()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(1, "2");

            lru.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be("2");
        }

        [Fact]
        public void WhenKeyExistsAddOrUpdateDisposesOldValue()
        {
            var lruOfDisposable = new ConcurrentLru<int, DisposableItem>(1, 6, EqualityComparer<int>.Default);
            var disposableValueFactory = new DisposableValueFactory();
            var newValue = new DisposableItem();

            lruOfDisposable.GetOrAdd(1, disposableValueFactory.Create);
            lruOfDisposable.AddOrUpdate(1, newValue);

            disposableValueFactory.Items[1].IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateMaintainsLruOrder()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");
            lru.AddOrUpdate(4, "4");

            lru.HotCount.Should().Be(3);
            lru.WarmCount.Should().Be(1); // items must have been enqueued and cycled for one of them to reach the warm queue
        }

        [Fact]
        public void WhenCacheIsEmptyClearIsNoOp()
        {
            lru.Clear();
            lru.Count.Should().Be(0);
        }

        [Fact]
        public void WhenItemsExistClearRemovesAllItems()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");

            lru.Clear();

            lru.Count.Should().Be(0);

            // verify queues are purged
            lru.HotCount.Should().Be(0);
            lru.WarmCount.Should().Be(0);
            lru.ColdCount.Should().Be(0);
        }

        [Fact]
        public void WhenItemsAreDisposableClearDisposesItemsOnRemove()
        {
            var lruOfDisposable = new ConcurrentLru<int, DisposableItem>(1, 6, EqualityComparer<int>.Default);

            var items = Enumerable.Range(1, 4).Select(i => new DisposableItem()).ToList();

            for (int i = 0; i < 4; i++)
            {
                lruOfDisposable.AddOrUpdate(i, items[i]);
            }

            lruOfDisposable.Clear();

            items.All(i => i.IsDisposed == true).Should().BeTrue();
        }

        [Fact]
        public void WhenItemsArClearedAnEventIsFired()
        {
            var lruEvents = new ConcurrentLru<int, int>(1, capacity, EqualityComparer<int>.Default);
            lruEvents.ItemRemoved += OnLruItemRemoved;

            for (int i = 0; i < 6; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => i + 1);
            }

            lruEvents.Clear();

            removedItems.Count.Should().Be(6);

            for (int i = 0; i < 6; i++)
            {
                removedItems[i].Reason.Should().Be(ItemRemovedReason.Cleared);
            }
        }

        [Fact]
        public void WhenTrimCountIsZeroThrows()
        {
            lru.Invoking(l => lru.Trim(0)).Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTrimCountIsMoreThanCapacityThrows()
        {
            lru.Invoking(l => lru.Trim(hotCap + warmCap + coldCap + 1)).Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(1, new[] { 9, 8, 7, 3, 2, 1, 6, 5 })]
        [InlineData(2, new[] { 9, 8, 7, 3, 2, 1, 6 })]
        [InlineData(3, new[] { 9, 8, 7, 3, 2, 1 })]
        [InlineData(4, new[] { 9, 8, 7, 3, 2 })]
        [InlineData(5, new[] { 9, 8, 7, 3 })]
        [InlineData(6, new[] { 9, 8, 7 })]
        [InlineData(7, new[] { 9, 8 })]
        [InlineData(8, new[] { 9 })]
        [InlineData(9, new int[] { })]
        public void WhenColdItemsExistTrimRemovesExpectedItemCount(int trimCount, int[] expected)
        {
            // initial state:
            // Hot = 9, 8, 7
            // Warm = 3, 2, 1
            // Cold = 6, 5, 4
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");
            lru.GetOrAdd(1, i => i.ToString());
            lru.GetOrAdd(2, i => i.ToString());
            lru.GetOrAdd(3, i => i.ToString());

            lru.AddOrUpdate(4, "4");
            lru.AddOrUpdate(5, "5");
            lru.AddOrUpdate(6, "6");

            lru.AddOrUpdate(7, "7");
            lru.AddOrUpdate(8, "8");
            lru.AddOrUpdate(9, "9");

            lru.Trim(trimCount);

            lru.Keys.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData(1, new[] { 6, 5, 4, 3, 2 })]
        [InlineData(2, new[] { 6, 5, 4, 3 })]
        [InlineData(3, new[] { 6, 5, 4 })]
        [InlineData(4, new[] { 6, 5 })]
        [InlineData(5, new[] { 6 })]
        [InlineData(6, new int[] { })]
        [InlineData(7, new int[] { })]
        [InlineData(8, new int[] { })]
        [InlineData(9, new int[] { })]
        public void WhenHotAndWarmItemsExistTrimRemovesExpectedItemCount(int itemCount, int[] expected)
        {
            // initial state:
            // Hot = 6, 5, 4
            // Warm = 3, 2, 1
            // Cold = -
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");
            lru.GetOrAdd(1, i => i.ToString());
            lru.GetOrAdd(2, i => i.ToString());
            lru.GetOrAdd(3, i => i.ToString());

            lru.AddOrUpdate(4, "4");
            lru.AddOrUpdate(5, "5");
            lru.AddOrUpdate(6, "6");

            lru.Trim(itemCount);

            lru.Keys.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData(1, new[] { 3, 2 })]
        [InlineData(2, new[] { 3 })]
        [InlineData(3, new int[] { })]
        [InlineData(4, new int[] { })]
        [InlineData(5, new int[] { })]
        [InlineData(6, new int[] { })]
        [InlineData(7, new int[] { })]
        [InlineData(8, new int[] { })]
        [InlineData(9, new int[] { })]
        public void WhenHotItemsExistTrimRemovesExpectedItemCount(int itemCount, int[] expected)
        {
            // initial state:
            // Hot = 3, 2, 1
            // Warm = -
            // Cold = -
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");

            lru.Trim(itemCount);

            lru.Keys.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData(1, new[] { 9, 8, 7, 6, 5, 4, 3, 2 })]
        [InlineData(2, new[] { 9, 8, 7, 6, 5, 4, 3 })]
        [InlineData(3, new[] { 9, 8, 7, 6, 5, 4 })]
        [InlineData(4, new[] { 9, 8, 7, 6, 5 })]
        [InlineData(5, new[] { 9, 8, 7, 6 })]
        [InlineData(6, new[] { 9, 8, 7 })]
        [InlineData(7, new[] { 9, 8 })]
        [InlineData(8, new[] { 9 })]
        [InlineData(9, new int[] { })]
        public void WhenColdItemsAreTouchedTrimRemovesExpectedItemCount(int trimCount, int[] expected)
        {
            // initial state:
            // Hot = 9, 8, 7
            // Warm = 3, 2, 1
            // Cold = 6*, 5*, 4*
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");
            lru.GetOrAdd(1, i => i.ToString());
            lru.GetOrAdd(2, i => i.ToString());
            lru.GetOrAdd(3, i => i.ToString());

            lru.AddOrUpdate(4, "4");
            lru.AddOrUpdate(5, "5");
            lru.AddOrUpdate(6, "6");

            lru.AddOrUpdate(7, "7");
            lru.AddOrUpdate(8, "8");
            lru.AddOrUpdate(9, "9");

            // touch all items in the cold queue
            lru.GetOrAdd(4, i => i.ToString());
            lru.GetOrAdd(5, i => i.ToString());
            lru.GetOrAdd(6, i => i.ToString());

            lru.Trim(trimCount);

            this.testOutputHelper.WriteLine("LRU " + string.Join(" ", lru.Keys));
            this.testOutputHelper.WriteLine("exp " + string.Join(" ", expected));

            lru.Keys.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void WhenItemsAreDisposableTrimDisposesItems()
        {
            var lruOfDisposable = new ConcurrentLru<int, DisposableItem>(1, new EqualCapacityPartition(6), EqualityComparer<int>.Default);

            var items = Enumerable.Range(1, 4).Select(i => new DisposableItem()).ToList();

            for (int i = 0; i < 4; i++)
            {
                lruOfDisposable.AddOrUpdate(i, items[i]);
            }

            lruOfDisposable.Trim(2);

            items[0].IsDisposed.Should().BeTrue();
            items[1].IsDisposed.Should().BeTrue();
            items[2].IsDisposed.Should().BeFalse();
            items[3].IsDisposed.Should().BeFalse();
        }

        [Fact]
        public void WhenItemsAreTrimmedAnEventIsFired()
        {
            var lruEvents = new ConcurrentLru<int, int>(1, capacity, EqualityComparer<int>.Default);
            lruEvents.ItemRemoved += OnLruItemRemoved;

            for (int i = 0; i < 6; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => i + 1);
            }

            lruEvents.Trim(2);

            removedItems.Count.Should().Be(2);

            removedItems[0].Key.Should().Be(1);
            removedItems[0].Value.Should().Be(2);
            removedItems[0].Reason.Should().Be(ItemRemovedReason.Trimmed);

            removedItems[1].Key.Should().Be(2);
            removedItems[1].Value.Should().Be(3);
            removedItems[1].Reason.Should().Be(ItemRemovedReason.Trimmed);
        }

        private void Warmup()
        {
            lru.GetOrAdd(-1, valueFactory.Create);
            lru.GetOrAdd(-2, valueFactory.Create);
            lru.GetOrAdd(-3, valueFactory.Create);
            lru.GetOrAdd(-4, valueFactory.Create);
            lru.GetOrAdd(-5, valueFactory.Create);
            lru.GetOrAdd(-6, valueFactory.Create);
            lru.GetOrAdd(-7, valueFactory.Create);
            lru.GetOrAdd(-8, valueFactory.Create);
            lru.GetOrAdd(-9, valueFactory.Create);
        }
    }
}
