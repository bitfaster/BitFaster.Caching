using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Atomic;
using Shouldly;
using Xunit;
using Moq;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class AtomicFactoryCacheTests
    {
        private const int capacity = 6;
        private readonly ConcurrentLru<int, AtomicFactory<int, int>> innerCache;
        private readonly AtomicFactoryCache<int, int> cache;

        private List<ItemRemovedEventArgs<int, int>> removedItems = new();
        private List<ItemUpdatedEventArgs<int, int>> updatedItems = new();

        public AtomicFactoryCacheTests()
        {
            innerCache = new ConcurrentLru<int, AtomicFactory<int, int>>(capacity);
            cache = new(innerCache);
        }

        [Fact]
        public void WhenInnerCacheIsNullCtorThrows()
        {
            Action constructor = () => { var x = new AtomicFactoryCache<int, int>(null); };

            constructor.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void WhenCreatedCapacityPropertyWrapsInnerCache()
        {
            this.cache.Policy.Eviction.Value.Capacity.ShouldBe(capacity);
        }

        [Fact]
        public void WhenItemIsAddedCountIsCorrect()
        {
            this.cache.Count.ShouldBe(0);

            this.cache.AddOrUpdate(2, 2);

            this.cache.Count.ShouldBe(1);
        }

        [Fact]
        public void WhenItemIsAddedThenLookedUpMetricsAreCorrect()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.GetOrAdd(1, k => k);

            this.cache.Metrics.Value.Misses.ShouldBe(0);
            this.cache.Metrics.Value.Hits.ShouldBe(1);
        }

        [Fact]
        public void WhenItemIsAddedWithArgValueIsCorrect()
        {
            this.cache.GetOrAdd(1, (k, a) => k + a, 2);

            this.cache.TryGet(1, out var value).ShouldBeTrue();
            value.ShouldBe(3);
        }

        [Fact]
        public void WhenRemovedEventHandlerIsRegisteredItIsFired()
        {
            this.cache.Events.Value.ItemRemoved += OnItemRemoved;

            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(1);

            this.removedItems.First().Key.ShouldBe(1);
        }

        // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenRemovedValueIsReturned()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(1, out var value);

            value.ShouldBe(1);
        }

        [Fact]
        public void WhenNotRemovedValueIsDefault()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(2, out var value);

            value.ShouldBe(0);
        }

        [Fact]
        public void WhenRemoveKeyValueAndValueDoesntMatchDontRemove()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(new KeyValuePair<int, int>(1, 2)).ShouldBeFalse();
        }

        [Fact]
        public void WhenRemoveKeyValueAndValueDoesMatchThenRemove()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(new KeyValuePair<int, int>(1, 1)).ShouldBeTrue();
        }

        [Fact]
        public void WhenRemoveKeyValueAndValueIsNotCreatedDoesNotRemove()
        {
            // seed the inner cache with an not yet created value
            this.innerCache.AddOrUpdate(1, new AtomicFactory<int, int>());

            // try to remove with the default value (0)
            this.cache.TryRemove(new KeyValuePair<int, int>(1, 0)).ShouldBeFalse();
        }

        [Fact]
        public void WhenUpdatedEventHandlerIsRegisteredItIsFired()
        {
            this.cache.Events.Value.ItemUpdated += OnItemUpdated;

            this.cache.AddOrUpdate(1, 2);
            this.cache.AddOrUpdate(1, 3);

            this.updatedItems.First().Key.ShouldBe(1);
            this.updatedItems.First().OldValue.ShouldBe(2);
            this.updatedItems.First().NewValue.ShouldBe(3);
        }
#endif
        [Fact]
        public void WhenNoInnerEventsNoOuterEvents()
        {
            var inner = new Mock<ICache<int, AtomicFactory<int, int>>>();
            inner.SetupGet(c => c.Events).Returns(Optional<ICacheEvents<int, AtomicFactory<int, int>>>.None);

            var cache = new AtomicFactoryCache<int, int>(inner.Object);

            cache.Events.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateAddsNewItem()
        {
            this.cache.AddOrUpdate(1, 1);

            this.cache.TryGet(1, out var value).ShouldBeTrue();
            value.ShouldBe(1);
        }

        [Fact]
        public void WhenKeyExistsAddOrUpdateUpdatesExistingItem()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.AddOrUpdate(1, 2);

            this.cache.TryGet(1, out var value).ShouldBeTrue();
            value.ShouldBe(2);
        }

        [Fact]
        public void WhenClearedItemsAreRemoved()
        {
            this.cache.AddOrUpdate(1, 1);

            this.cache.Clear();

            this.cache.Count.ShouldBe(0);
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetReturnsFalse()
        {
            this.cache.TryGet(1, out var value).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistGetOrAddAddsValue()
        {
            this.cache.GetOrAdd(1, k => k);

            this.cache.TryGet(1, out var value).ShouldBeTrue();
            value.ShouldBe(1);
        }

        [Fact]
        public void WhenCacheContainsValuesTrim1RemovesColdestValue()
        {
            this.cache.AddOrUpdate(0, 0);
            this.cache.AddOrUpdate(1, 1);
            this.cache.AddOrUpdate(2, 2);

            this.cache.Policy.Eviction.Value.Trim(1);

            this.cache.TryGet(0, out var value).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveReturnsFalse()
        {
            this.cache.TryRemove(1).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryRemoveReturnsTrue()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(1).ShouldBeTrue();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryUpdateReturnsFalse()
        {
            this.cache.TryUpdate(1, 1).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryUpdateReturnsTrue()
        {
            this.cache.AddOrUpdate(1, 1);

            this.cache.TryUpdate(1, 2).ShouldBeTrue();
            this.cache.TryGet(1, out var value);
            value.ShouldBe(2);
        }

        [Fact]
        public void WhenItemsAddedKeysContainsTheKeys()
        {
            cache.Count.ShouldBe(0);
            cache.AddOrUpdate(1, 1);
            cache.AddOrUpdate(2, 2);
            cache.Keys.ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void WhenItemsAddedGenericEnumerateContainsKvps()
        {
            cache.Count.ShouldBe(0);
            cache.AddOrUpdate(1, 1);
            cache.AddOrUpdate(2, 2);
            cache.ShouldBe(new[] { new KeyValuePair<int, int>(1, 1), new KeyValuePair<int, int>(2, 2) });
        }

        [Fact]
        public void WhenItemsAddedEnumerateContainsKvps()
        {
            cache.Count.ShouldBe(0);
            cache.AddOrUpdate(1, 1);
            cache.AddOrUpdate(2, 2);

            var enumerable = (IEnumerable)cache;
            enumerable.ShouldBe(new[] { new KeyValuePair<int, int>(1, 1), new KeyValuePair<int, int>(2, 2) });
        }

        [Fact]
        public void WhenFactoryThrowsEmptyValueIsNotCounted()
        {
            try
            {
                cache.GetOrAdd(1, _ => throw new Exception());
            }
            catch { }

            cache.Count.ShouldBe(0);
        }

        [Fact]
        public void WhenFactoryThrowsEmptyValueIsNotEnumerable()
        {
            try
            {
                cache.GetOrAdd(1, k => throw new Exception());
            }
            catch { }

            // IEnumerable.Count() instead of Count property
            cache.Count().ShouldBe(0);
        }

        [Fact]
        public void WhenFactoryThrowsEmptyKeyIsNotEnumerable()
        {
            try
            {
                cache.GetOrAdd(1, k => throw new Exception());
            }
            catch { }

            cache.Keys.Count().ShouldBe(0);
        }

        private void OnItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            this.removedItems.Add(e);
        }

        private void OnItemUpdated(object sender, ItemUpdatedEventArgs<int, int> e)
        {
            this.updatedItems.Add(e);
        }
    }
}
