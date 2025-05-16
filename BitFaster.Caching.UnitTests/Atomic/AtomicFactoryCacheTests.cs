﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Atomic;
using FluentAssertions;
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

            constructor.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenCreatedCapacityPropertyWrapsInnerCache()
        {
            this.cache.Policy.Eviction.Value.Capacity.Should().Be(capacity);
        }

        [Fact]
        public void WhenItemIsAddedCountIsCorrect()
        {
            this.cache.Count.Should().Be(0);

            this.cache.AddOrUpdate(2, 2);

            this.cache.Count.Should().Be(1);
        }

        [Fact]
        public void WhenItemIsAddedThenLookedUpMetricsAreCorrect()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.GetOrAdd(1, k => k);

            this.cache.Metrics.Value.Misses.Should().Be(0);
            this.cache.Metrics.Value.Hits.Should().Be(1);
        }

        [Fact]
        public void WhenItemIsAddedWithArgValueIsCorrect()
        {
            this.cache.GetOrAdd(1, (k, a) => k + a, 2);

            this.cache.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(3);
        }

        [Fact]
        public void WhenRemovedEventHandlerIsRegisteredItIsFired()
        {
            this.cache.Events.Value.ItemRemoved += OnItemRemoved;

            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(1);

            this.removedItems.First().Key.Should().Be(1);
        }

// backcompat: remove conditional compile
#if NET
        [Fact]
        public void WhenRemovedValueIsReturned()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(1, out var value);

            value.Should().Be(1);
        }

        [Fact]
        public void WhenNotRemovedValueIsDefault()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(2, out var value);

            value.Should().Be(0);
        }

        [Fact]
        public void WhenRemoveKeyValueAndValueDoesntMatchDontRemove()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(new KeyValuePair<int, int>(1, 2)).Should().BeFalse();
        }

        [Fact]
        public void WhenRemoveKeyValueAndValueDoesMatchThenRemove()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(new KeyValuePair<int, int>(1, 1)).Should().BeTrue();
        }

        [Fact]
        public void WhenRemoveKeyValueAndValueIsNotCreatedDoesNotRemove()
        {
            // seed the inner cache with an not yet created value
            this.innerCache.AddOrUpdate(1, new AtomicFactory<int, int>());

            // try to remove with the default value (0)
            this.cache.TryRemove(new KeyValuePair<int, int>(1, 0)).Should().BeFalse();
        }

        [Fact]
        public void WhenUpdatedEventHandlerIsRegisteredItIsFired()
        {
            this.cache.Events.Value.ItemUpdated += OnItemUpdated;

            this.cache.AddOrUpdate(1, 2);
            this.cache.AddOrUpdate(1, 3);

            this.updatedItems.First().Key.Should().Be(1);
            this.updatedItems.First().OldValue.Should().Be(2);
            this.updatedItems.First().NewValue.Should().Be(3);
        }
#endif
        [Fact]
        public void WhenNoInnerEventsNoOuterEvents()
        {
            var inner = new Mock<ICache<int, AtomicFactory<int, int>>>();
            inner.SetupGet(c => c.Events).Returns(Optional<ICacheEvents<int, AtomicFactory<int, int>>>.None);

            var cache = new AtomicFactoryCache<int, int>(inner.Object);

            cache.Events.HasValue.Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateAddsNewItem()
        {
            this.cache.AddOrUpdate(1, 1);

            this.cache.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(1);
        }

        [Fact]
        public void WhenKeyExistsAddOrUpdateUpdatesExistingItem()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.AddOrUpdate(1, 2);

            this.cache.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(2);
        }

        [Fact]
        public void WhenClearedItemsAreRemoved()
        {
            this.cache.AddOrUpdate(1, 1);

            this.cache.Clear();

            this.cache.Count.Should().Be(0);
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetReturnsFalse()
        {
            this.cache.TryGet(1, out var value).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistGetOrAddAddsValue()
        {
            this.cache.GetOrAdd(1, k => k);

            this.cache.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(1);
        }

        [Fact]
        public void WhenCacheContainsValuesTrim1RemovesColdestValue()
        {
            this.cache.AddOrUpdate(0, 0);
            this.cache.AddOrUpdate(1, 1);
            this.cache.AddOrUpdate(2, 2);

            this.cache.Policy.Eviction.Value.Trim(1);

            this.cache.TryGet(0, out var value).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveReturnsFalse()
        {
            this.cache.TryRemove(1).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryRemoveReturnsTrue()
        {
            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(1).Should().BeTrue();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryUpdateReturnsFalse()
        {
            this.cache.TryUpdate(1, 1).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryUpdateReturnsTrue()
        {
            this.cache.AddOrUpdate(1, 1);

            this.cache.TryUpdate(1, 2).Should().BeTrue();
            this.cache.TryGet(1, out var value);
            value.Should().Be(2);
        }

        [Fact]
        public void WhenItemsAddedKeysContainsTheKeys()
        {
            cache.Count.Should().Be(0);
            cache.AddOrUpdate(1, 1);
            cache.AddOrUpdate(2, 2);
            cache.Keys.Should().BeEquivalentTo(new[] { 1, 2 });
        }

        [Fact]
        public void WhenItemsAddedGenericEnumerateContainsKvps()
        {
            cache.Count.Should().Be(0);
            cache.AddOrUpdate(1, 1);
            cache.AddOrUpdate(2, 2);
            cache.Should().BeEquivalentTo(new[] { new KeyValuePair<int, int>(1, 1), new KeyValuePair<int, int>(2, 2) });
        }

        [Fact]
        public void WhenItemsAddedEnumerateContainsKvps()
        {
            cache.Count.Should().Be(0);
            cache.AddOrUpdate(1, 1);
            cache.AddOrUpdate(2, 2);

            var enumerable = (IEnumerable)cache;
            enumerable.Should().BeEquivalentTo(new[] { new KeyValuePair<int, int>(1, 1), new KeyValuePair<int, int>(2, 2) });
        }

        [Fact]
        public void WhenFactoryThrowsEmptyValueIsNotCounted()
        {
            try
            {
                cache.GetOrAdd(1, _ => throw new Exception());
            }
            catch { }

            cache.Count.Should().Be(0);
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
            cache.Count().Should().Be(0);
        }

        [Fact]
        public void WhenFactoryThrowsEmptyKeyIsNotEnumerable()
        {
            try
            {
                cache.GetOrAdd(1, k => throw new Exception());
            }
            catch { }

            cache.Keys.Count().Should().Be(0);
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
