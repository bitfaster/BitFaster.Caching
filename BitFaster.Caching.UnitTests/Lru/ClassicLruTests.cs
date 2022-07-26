using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenCapacityIsLessThan3CtorThrows()
        {
            Action constructor = () => { var x = new ClassicLru<int, string>(1, 2, EqualityComparer<int>.Default); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenComparerIsNullCtorThrows()
        {
            Action constructor = () => { var x = new ClassicLru<int, string>(1, 3, null); };

            constructor.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ConstructAddAndRetrieveWithDefaultCtorReturnsValue()
        {
            var x = new ClassicLru<int, int>(3);

            x.GetOrAdd(1, k => k).Should().Be(1);
        }

        [Fact]
        public void WhenCtorCapacityArgIs3CapacityIs3()
        {
            new ClassicLru<int, int>(3).Capacity.Should().Be(3);
        }

        [Fact]
        public void WhenItemIsAddedCountIsCorrect()
        {
            lru.Count.Should().Be(0);
            lru.GetOrAdd(1, valueFactory.Create);
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
        public void MetricsAreEnabled()
        {
            lru.Metrics.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedMetricHitRatioIsHalf()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.Metrics.HitRatio.Should().Be(0.5);
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedMetricHitsIs1()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.Metrics.Hits.Should().Be(1);
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedMetricTotalIs2()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.Metrics.Total.Should().Be(2);
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetIncrementsMiss()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.Metrics.Misses.Should().Be(1);
        }

        [Fact]
        public void EventsAreEnabled()
        {
            lru.Events.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void RegisterAndUnregisterIsNoOp()
        {
            lru.Events.ItemRemoved += OnItemRemoved;
            lru.Events.ItemRemoved -= OnItemRemoved;
        }

        private void OnItemRemoved(object sender, ItemRemovedEventArgs<int, string> e)
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void ExpireAfterWriteIsDisabled()
        {
            lru.Policy.ExpireAfterWrite.Should().Be(NoneTimePolicy.Instance);
            lru.Policy.ExpireAfterWrite.CanExpire.Should().BeFalse();
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
        public void WhenMoreKeysRequestedThanCapacityCountDoesNotIncrease()
        {
            for (int i = 0; i < capacity + 1; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);
            }

            lru.Count.Should().Be(capacity);
            valueFactory.timesCalled.Should().Be(capacity + 1);
        }

        [Fact]
        public async Task WhenMoreKeysRequestedThanCapacityCountDoesNotIncreaseAsync()
        {
            for (int i = 0; i < capacity + 1; i++)
            {
                await lru.GetOrAddAsync(i, valueFactory.CreateAsync);
            }

            lru.Count.Should().Be(capacity);
            valueFactory.timesCalled.Should().Be(capacity + 1);
        }

        [Fact]
        public void WhenMoreKeysRequestedThanCapacityOldestItemIsEvicted()
        {
            // request 10 items, LRU is now full
            for (int i = 0; i < capacity; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);
            }

            valueFactory.timesCalled.Should().Be(capacity);

            // request 0, now item 1 is to be evicted
            lru.GetOrAdd(0, valueFactory.Create);
            valueFactory.timesCalled.Should().Be(capacity);

            // request next item after last, verify value factory was called
            lru.GetOrAdd(capacity, valueFactory.Create);
            valueFactory.timesCalled.Should().Be(capacity + 1);

            // request 0, verify value factory not called
            lru.GetOrAdd(0, valueFactory.Create);
            valueFactory.timesCalled.Should().Be(capacity + 1);

            // request 1, verify value factory is called (and it was therefore not cached)
            lru.GetOrAdd(1, valueFactory.Create);
            valueFactory.timesCalled.Should().Be(capacity + 2);
        }

        [Fact]
        public void WhenMoreKeysRequestedThanCapacityEvictedMetricRecordsNumberEvicted()
        {
            // request 3 items, LRU is now full
            for (int i = 0; i < capacity; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);
            }

            lru.Metrics.Evicted.Should().Be(0);

            // request 0, now item 1 is to be evicted
            lru.GetOrAdd(4, valueFactory.Create);

            lru.Metrics.Evicted.Should().Be(1);
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

            disposableValueFactory.Items[0].IsDisposed.Should().BeTrue();
            disposableValueFactory.Items[1].IsDisposed.Should().BeFalse();
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

            disposableValueFactory.Items[0].IsDisposed.Should().BeTrue();
            disposableValueFactory.Items[1].IsDisposed.Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryGetReturnsFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryGet(2, out var result).Should().Be(false);
        }

        [Fact]
        public void WhenKeyExistsTryGetReturnsTrueAndOutValueIsCorrect()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            bool result = lru.TryGet(1, out var value);
            result.Should().Be(true);
            value.Should().Be("1");
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
            var lruOfDisposable = new ClassicLru<int, DisposableItem>(1, 6, EqualityComparer<int>.Default);
            var disposableValueFactory = new DisposableValueFactory();

            lruOfDisposable.GetOrAdd(1, disposableValueFactory.Create);
            lruOfDisposable.TryRemove(1);

            disposableValueFactory.Items[1].IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveReturnsFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryRemove(2).Should().BeFalse();
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
        public void WhenKeyExistsAddOrUpdatUpdatesExistingItem()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(1, "2");

            lru.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be("2");
        }

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateMaintainsLruOrder()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");
            lru.AddOrUpdate(4, "4");

            // verify first item added is removed
            lru.Count.Should().Be(3);
            lru.TryGet(1, out var value).Should().BeFalse();
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
            items[0].IsDisposed.Should().BeTrue();

            // all other items are not disposed
            items.Skip(1).All(i => i.IsDisposed == false).Should().BeTrue();
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

            items.All(i => i.IsDisposed == true).Should().BeTrue();
        }

        [Fact]
        public void WhenTrimCountIsZeroThrows()
        { 
            lru.Invoking(l => lru.Trim(0)).Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenTrimCountIsMoreThanCapacityThrows()
        {
            lru.Invoking(l => lru.Trim(capacity + 1)).Should().Throw<ArgumentOutOfRangeException>();
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

            lru.Keys.Should().BeEquivalentTo(expected);
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

            items[0].IsDisposed.Should().BeTrue();
            items[1].IsDisposed.Should().BeTrue();
            items[2].IsDisposed.Should().BeFalse();
            items[3].IsDisposed.Should().BeFalse();
        }
    }
}
