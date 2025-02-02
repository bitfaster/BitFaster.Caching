using Shouldly;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using System.Collections;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ClassicLruTests
    {
        private const int capacity = 3;

        private ClassicLru<int, string> lru = new ClassicLru<int, string>(1, capacity, EqualityComparer<int>.Default);
        ValueFactory valueFactory = new ValueFactory();

        [Fact]
        public void WhenConcurrencyIsLessThan1CtorThrows()
        {
            Action constructor = () => { var x = new ClassicLru<int, string>(0, 3, EqualityComparer<int>.Default); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenCapacityIsLessThan3CtorThrows()
        {
            Action constructor = () => { var x = new ClassicLru<int, string>(1, 2, EqualityComparer<int>.Default); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenComparerIsNullCtorThrows()
        {
            Action constructor = () => { var x = new ClassicLru<int, string>(1, 3, null); };

            constructor.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ConstructAddAndRetrieveWithDefaultCtorReturnsValue()
        {
            var x = new ClassicLru<int, int>(3);

            x.GetOrAdd(1, k => k).ShouldBe(1);
        }

        [Fact]
        public void WhenCtorCapacityArgIs3CapacityIs3()
        {
            new ClassicLru<int, int>(3).Capacity.ShouldBe(3);
        }

        [Fact]
        public void WhenItemIsAddedCountIsCorrect()
        {
            lru.Count.ShouldBe(0);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.Count.ShouldBe(1);
        }

        [Fact]
        public void WhenItemsAddedKeysContainsTheKeys()
        {
            lru.Count.ShouldBe(0);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.Keys.ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void WhenItemsAddedGenericEnumerateContainsKvps()
        {
            lru.Count.ShouldBe(0);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.ShouldBe(new[] { new KeyValuePair<int, string>(1, "1"), new KeyValuePair<int, string>(2, "2") });
        }

        [Fact]
        public void WhenItemsAddedEnumerateContainsKvps()
        {
            lru.Count.ShouldBe(0);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);

            var enumerable = (IEnumerable)lru;
            enumerable.ShouldBe(new[] { new KeyValuePair<int, string>(1, "1"), new KeyValuePair<int, string>(2, "2") });
        }

        [Fact]
        public void WhenItemExistsTryGetReturnsValueAndTrue()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            result.ShouldBe(true);
            value.ShouldBe("1");
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetReturnsNullAndFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(2, out var value);

            result.ShouldBe(false);
            value.ShouldBeNull();
        }

        [Fact]
        public void MetricsAreEnabled()
        {
            lru.Metrics.HasValue.ShouldBeTrue();
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedMetricHitRatioIsHalf()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.Metrics.Value.HitRatio.ShouldBe(0.5);
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedMetricHitsIs1()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.Metrics.Value.Hits.ShouldBe(1);
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedMetricTotalIs2()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.Metrics.Value.Total.ShouldBe(2);
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetIncrementsMiss()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.Metrics.Value.Misses.ShouldBe(1);
        }

        [Fact]
        public void EventsAreEnabled()
        {
            lru.Events.HasValue.ShouldBeFalse();
        }

        private void OnItemRemoved(object sender, ItemRemovedEventArgs<int, string> e)
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void ExpireAfterWriteIsDisabled()
        {
            lru.Policy.ExpireAfterWrite.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyIsRequestedItIsCreatedAndCached()
        {
            var result1 = lru.GetOrAdd(1, valueFactory.Create);
            var result2 = lru.GetOrAdd(1, valueFactory.Create);

            valueFactory.timesCalled.ShouldBe(1);
            result1.ShouldBe(result2);
        }


        [Fact]
        public void WhenKeyIsRequestedWithArgItIsCreatedAndCached()
        {
            var result1 = lru.GetOrAdd(1, valueFactory.Create, "x");
            var result2 = lru.GetOrAdd(1, valueFactory.Create, "y");

            valueFactory.timesCalled.ShouldBe(1);
            result1.ShouldBe(result2);
        }

        [Fact]
        public async Task WhenKeyIsRequesteItIsCreatedAndCachedAsync()
        {
            var result1 = await lru.GetOrAddAsync(1, valueFactory.CreateAsync);
            var result2 = await lru.GetOrAddAsync(1, valueFactory.CreateAsync);

            valueFactory.timesCalled.ShouldBe(1);
            result1.ShouldBe(result2);
        }

        [Fact]
        public async Task WhenKeyIsRequestedWithArgItIsCreatedAndCachedAsync()
        {
            var result1 = await lru.GetOrAddAsync(1, valueFactory.CreateAsync, "x");
            var result2 = await lru.GetOrAddAsync(1, valueFactory.CreateAsync, "y");

            valueFactory.timesCalled.ShouldBe(1);
            result1.ShouldBe(result2);
        }

        [Fact]
        public void WhenDifferentKeysAreRequestedValueIsCreatedForEach()
        {
            var result1 = lru.GetOrAdd(1, valueFactory.Create);
            var result2 = lru.GetOrAdd(2, valueFactory.Create);

            valueFactory.timesCalled.ShouldBe(2);

            result1.ShouldBe("1");
            result2.ShouldBe("2");
        }

        [Fact]
        public async Task WhenDifferentKeysAreRequesteValueIsCreatedForEachAsync()
        {
            var result1 = await lru.GetOrAddAsync(1, valueFactory.CreateAsync);
            var result2 = await lru.GetOrAddAsync(2, valueFactory.CreateAsync);

            valueFactory.timesCalled.ShouldBe(2);

            result1.ShouldBe("1");
            result2.ShouldBe("2");
        }

        [Fact]
        public void WhenMoreKeysRequestedThanCapacityCountDoesNotIncrease()
        {
            for (int i = 0; i < capacity + 1; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);
            }

            lru.Count.ShouldBe(capacity);
            valueFactory.timesCalled.ShouldBe(capacity + 1);
        }

        [Fact]
        public async Task WhenMoreKeysRequestedThanCapacityCountDoesNotIncreaseAsync()
        {
            for (int i = 0; i < capacity + 1; i++)
            {
                await lru.GetOrAddAsync(i, valueFactory.CreateAsync);
            }

            lru.Count.ShouldBe(capacity);
            valueFactory.timesCalled.ShouldBe(capacity + 1);
        }

        [Fact]
        public void WhenMoreKeysRequestedThanCapacityOldestItemIsEvicted()
        {
            // request 10 items, LRU is now full
            for (int i = 0; i < capacity; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);
            }

            valueFactory.timesCalled.ShouldBe(capacity);

            // request 0, now item 1 is to be evicted
            lru.GetOrAdd(0, valueFactory.Create);
            valueFactory.timesCalled.ShouldBe(capacity);

            // request next item after last, verify value factory was called
            lru.GetOrAdd(capacity, valueFactory.Create);
            valueFactory.timesCalled.ShouldBe(capacity + 1);

            // request 0, verify value factory not called
            lru.GetOrAdd(0, valueFactory.Create);
            valueFactory.timesCalled.ShouldBe(capacity + 1);

            // request 1, verify value factory is called (and it was therefore not cached)
            lru.GetOrAdd(1, valueFactory.Create);
            valueFactory.timesCalled.ShouldBe(capacity + 2);
        }

        [Fact]
        public void WhenMoreKeysRequestedThanCapacityEvictedMetricRecordsNumberEvicted()
        {
            // request 3 items, LRU is now full
            for (int i = 0; i < capacity; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);
            }

            lru.Metrics.Value.Evicted.ShouldBe(0);

            // request 0, now item 1 is to be evicted
            lru.GetOrAdd(4, valueFactory.Create);

            lru.Metrics.Value.Evicted.ShouldBe(1);
        }

        [Fact]
        public void WhenValueExpiresItIsDisposed()
        {
            var lruOfDisposable = new ClassicLru<int, DisposableItem>(1, 6, EqualityComparer<int>.Default);
            var disposableValueFactory = new DisposableValueFactory();

            for (int i = 0; i < 7; i++)
            {
                lruOfDisposable.GetOrAdd(i, disposableValueFactory.Create);
            }

            disposableValueFactory.Items[0].IsDisposed.ShouldBeTrue();
            disposableValueFactory.Items[1].IsDisposed.ShouldBeFalse();
        }

        [Fact]
        public async Task WhenValueExpiresAsyncItIsDisposed()
        {
            var lruOfDisposable = new ClassicLru<int, DisposableItem>(1, 6, EqualityComparer<int>.Default);
            var disposableValueFactory = new DisposableValueFactory();

            for (int i = 0; i < 7; i++)
            {
                await lruOfDisposable.GetOrAddAsync(i, disposableValueFactory.CreateAsync);
            }

            disposableValueFactory.Items[0].IsDisposed.ShouldBeTrue();
            disposableValueFactory.Items[1].IsDisposed.ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryGetReturnsFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryGet(2, out var result).ShouldBe(false);
        }

        [Fact]
        public void WhenKeyExistsTryGetReturnsTrueAndOutValueIsCorrect()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            bool result = lru.TryGet(1, out var value);
            result.ShouldBe(true);
            value.ShouldBe("1");
        }

        [Fact]
        public void WhenKeyExistsTryRemoveRemovesItemAndReturnsTrue()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryRemove(1).ShouldBeTrue();
            lru.TryGet(1, out var value).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryRemoveReturnsValue()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryRemove(1, out var value).ShouldBeTrue();
            value.ShouldBe("1");
        }

        [Fact]
        public void WhenItemExistsTryRemovesItemAndReturnsTrue()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryRemove(new KeyValuePair<int, string>(1, "1")).ShouldBeTrue();
            lru.TryGet(1, out var value).ShouldBeFalse();
        }

        [Fact]
        public void WhenTryRemoveKvpDoesntMatchItemNotRemovedAndReturnsFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryRemove(new KeyValuePair<int, string>(1, "2")).ShouldBeFalse();
            lru.TryGet(1, out var value).ShouldBeTrue();
        }

        [Fact]
        public void WhenItemIsRemovedItIsDisposed()
        {
            var lruOfDisposable = new ClassicLru<int, DisposableItem>(1, 6, EqualityComparer<int>.Default);
            var disposableValueFactory = new DisposableValueFactory();

            lruOfDisposable.GetOrAdd(1, disposableValueFactory.Create);
            lruOfDisposable.TryRemove(1);

            disposableValueFactory.Items[1].IsDisposed.ShouldBeTrue();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveReturnsFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryRemove(2).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryUpdateUpdatesValueAndReturnsTrue()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryUpdate(1, "2").ShouldBeTrue();

            lru.TryGet(1, out var value);
            value.ShouldBe("2");
        }

        [Fact]
        public void WhenKeyDoesNotExistTryUpdateReturnsFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryUpdate(2, "3").ShouldBeFalse();
        }

// backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenKeyExistsTryUpdateIncrementsUpdateCount()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryUpdate(1, "2").ShouldBeTrue();

            lru.Metrics.Value.Updated.ShouldBe(1);
        }

        [Fact]
        public void WhenKeyDoesNotExistTryUpdateDoesNotIncrementCounter()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryUpdate(2, "3").ShouldBeFalse();

            lru.Metrics.Value.Updated.ShouldBe(0);
        }
#endif

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateAddsNewItem()
        {
            lru.AddOrUpdate(1, "1");

            lru.TryGet(1, out var value).ShouldBeTrue();
            value.ShouldBe("1");
        }

        [Fact]
        public void WhenKeyExistsAddOrUpdatUpdatesExistingItem()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(1, "2");

            lru.TryGet(1, out var value).ShouldBeTrue();
            value.ShouldBe("2");
        }

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateMaintainsLruOrder()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");
            lru.AddOrUpdate(4, "4");

            // verify first item added is removed
            lru.Count.ShouldBe(3);
            lru.TryGet(1, out _).ShouldBeFalse();
        }

        [Fact]
        public void WhenAddOrUpdateExpiresItemsTheyAreDisposed()
        {
            var lruOfDisposable = new ClassicLru<int, DisposableItem>(1, 3, EqualityComparer<int>.Default);

            var items = Enumerable.Range(1, 4).Select(i => new DisposableItem()).ToList();

            for (int i = 0; i < 4; i++)
            {
                lruOfDisposable.AddOrUpdate(i, items[i]);
            }

            // first item is evicted and disposed
            items[0].IsDisposed.ShouldBeTrue();

            // all other items are not disposed
            items.Skip(1).All(i => i.IsDisposed == false).ShouldBeTrue();
        }

        [Fact]
        public void WhenCacheIsEmptyClearIsNoOp()
        {
            lru.Clear();
            lru.Count.ShouldBe(0);
        }

        [Fact]
        public void WhenItemsExistClearRemovesAllItems()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.Clear();
            lru.Count.ShouldBe(0);
        }

        [Fact]
        public void WhenItemsAreDisposableClearDisposesItemsOnRemove()
        {
            var lruOfDisposable = new ClassicLru<int, DisposableItem>(1, 3, EqualityComparer<int>.Default);

            var items = Enumerable.Range(1, 4).Select(i => new DisposableItem()).ToList();

            for (int i = 0; i < 4; i++)
            {
                lruOfDisposable.AddOrUpdate(i, items[i]);
            }

            lruOfDisposable.Clear();

            items.All(i => i.IsDisposed == true).ShouldBeTrue();
        }

        [Fact]
        public void WhenTrimCountIsZeroThrows()
        {
            Should.Throw<ArgumentOutOfRangeException>(() => lru.Trim(0));
        }

        [Fact]
        public void WhenTrimCountIsMoreThanCapacityThrows()
        {
            Should.Throw<ArgumentOutOfRangeException>(() => lru.Trim(capacity + 1));
        }

        [Theory]
        [InlineData(1, new[] { 1, 3 })]
        [InlineData(2, new[] { 1 })]
        [InlineData(3, new int[] { })]
        public void WhenItemsExistTrimRemovesExpectedItemCount(int trimCount, int[] expected)
        {
            // initial state:
            // 1, 3, 2
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");

            lru.GetOrAdd(1, i => i.ToString());

            lru.Trim(trimCount);

            lru.Keys.ShouldBe(expected);
        }

        [Fact]
        public void WhenCacheIsEmptyTrimIsNoOp()
        {
            lru.Trim(2);
        }

        [Fact]
        public void WhenItemsAreDisposableTrimDisposesItems()
        {
            var lruOfDisposable = new ClassicLru<int, DisposableItem>(1, 4, EqualityComparer<int>.Default);

            var items = Enumerable.Range(1, 4).Select(i => new DisposableItem()).ToList();

            for (int i = 0; i < 4; i++)
            {
                lruOfDisposable.AddOrUpdate(i, items[i]);
            }

            lruOfDisposable.Trim(2);

            items[0].IsDisposed.ShouldBeTrue();
            items[1].IsDisposed.ShouldBeTrue();
            items[2].IsDisposed.ShouldBeFalse();
            items[3].IsDisposed.ShouldBeFalse();
        }
    }
}
