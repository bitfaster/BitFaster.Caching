using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentLruTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private const int hotCap = 3;
        private const int warmCap = 3;
        private const int coldCap = 3;

        private ConcurrentLru<int, string> lru = new ConcurrentLru<int, string>(1, hotCap + warmCap + coldCap, EqualityComparer<int>.Default);
        private ValueFactory valueFactory = new ValueFactory();

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
        public void WhenComparerIsNullCtorThrows()
        {
            Action constructor = () => { var x = new ConcurrentLru<int, string>(1, 3, null); };

            constructor.Should().Throw<ArgumentNullException>();
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

            lru.HitRatio.Should().Be(0.5);
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
            int hotColdCapacity = hotCap + coldCap;
            for (int i = 0; i < hotColdCapacity + 1; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);
            }

            lru.Count.Should().Be(hotColdCapacity);
            valueFactory.timesCalled.Should().Be(hotColdCapacity + 1);
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
            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            lru.HotCount.Should().Be(3);
            lru.WarmCount.Should().Be(0);
            lru.ColdCount.Should().Be(1);
        }

        [Fact]
        public void WhenValueIsTouchedAndExpiresFromHotValueIsBumpedToWarm()
        {
            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(0, valueFactory.Create);

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            lru.HotCount.Should().Be(3);
            lru.WarmCount.Should().Be(1);
            lru.ColdCount.Should().Be(0);
        }

        [Fact]
        public void WhenValueIsTouchedAndExpiresFromColdItIsBumpedToWarm()
        {
            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            // touch 0 while it is in cold
            lru.GetOrAdd(0, valueFactory.Create);

            lru.GetOrAdd(4, valueFactory.Create);
            lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create);

            lru.HotCount.Should().Be(3);
            lru.WarmCount.Should().Be(1);
            lru.ColdCount.Should().Be(3);
        }

        [Fact]
        public void WhenValueIsNotTouchedAndExpiresFromColdItIsRemoved()
        {
            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);
            lru.GetOrAdd(4, valueFactory.Create);
            lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create);

            // insert 7, 0th item will expire from cold
            lru.HotCount.Should().Be(3);
            lru.WarmCount.Should().Be(0);
            lru.ColdCount.Should().Be(3);

            lru.TryGet(0, out var value).Should().Be(false);
        }

        [Fact]
        public void WhenValueIsNotTouchedAndExpiresFromWarmValueIsBumpedToCold()
        {
            // first 4 values are touched in hot, promote to warm
            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            // 3 values added to hot fill warm
            lru.GetOrAdd(4, valueFactory.Create);
            lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create);

            lru.HotCount.Should().Be(3);
            lru.WarmCount.Should().Be(3);
            lru.ColdCount.Should().Be(1);
        }

        [Fact]
        public void WhenValueIsTouchedAndExpiresFromWarmValueIsBumpedBackIntoWarm()
        {
            // first 4 values are touched in hot, promote to warm
            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(0, valueFactory.Create);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            // touch 0 while it is warm
            lru.GetOrAdd(0, valueFactory.Create);

            // 3 values added to hot fill warm. Only 0 is touched.
            lru.GetOrAdd(4, valueFactory.Create);
            lru.GetOrAdd(5, valueFactory.Create);
            lru.GetOrAdd(6, valueFactory.Create);

            // When warm fills, 2 items are processed. 1 is promoted back into warm, and 1 into cold.
            lru.HotCount.Should().Be(3);
            lru.WarmCount.Should().Be(3);
            lru.ColdCount.Should().Be(1);
        }

        [Fact]
        public void WhenValueExpiresItIsDisposed()
        {
            var lruOfDisposable = new ConcurrentLru<int, DisposableItem>(1, 6, EqualityComparer<int>.Default);
            var disposableValueFactory = new DisposableValueFactory();

            for (int i = 0; i < 5; i++)
            {
                lruOfDisposable.GetOrAdd(i, disposableValueFactory.Create);
            }

            disposableValueFactory.Items[0].IsDisposed.Should().BeTrue();
            disposableValueFactory.Items[1].IsDisposed.Should().BeFalse();
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
        public void WhenKeyDoesNotExistTryUpdateReturnsFalse()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryUpdate(2, "3").Should().BeFalse();
        }
    }
}
