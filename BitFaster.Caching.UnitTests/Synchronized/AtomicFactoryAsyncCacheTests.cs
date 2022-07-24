using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Synchronized;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Synchronized
{
    public class AtomicFactoryAsyncCacheTests
    {
        private const int capacity = 6;
        private readonly AtomicFactoryAsyncCache<int, int> cache = new(new ConcurrentLru<int, AsyncAtomicFactory<int, int>>(capacity));

        private List<ItemRemovedEventArgs<int, int>> removedItems = new();

        [Fact]
        public void WhenInnerCacheIsNullCtorThrows()
        {
            Action constructor = () => { var x = new AtomicFactoryAsyncCache<int, int>(null); };

            constructor.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenCreatedCapacityPropertyWrapsInnerCache()
        {
            this.cache.Capacity.Should().Be(capacity);
        }

        [Fact]
        public void WhenItemIsAddedCountIsCorrect()
        {
            this.cache.Count.Should().Be(0);

            this.cache.AddOrUpdate(2, 2);

            this.cache.Count.Should().Be(1);
        }

        [Fact]
        public async Task WhenItemIsAddedThenLookedUpMetricsAreCorrect()
        {
            this.cache.AddOrUpdate(1, 1);
            await this.cache.GetOrAddAsync(1, k => Task.FromResult(k));

            this.cache.Metrics.Misses.Should().Be(0);
            this.cache.Metrics.Hits.Should().Be(1);
        }

        [Fact]
        public void WhenEventHandlerIsRegisteredItIsFired()
        {
            this.cache.Events.ItemRemoved += OnItemRemoved;

            this.cache.AddOrUpdate(1, 1);
            this.cache.TryRemove(1);

            this.removedItems.First().Key.Should().Be(1);
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
        public async Task WhenKeyDoesNotExistGetOrAddAsyncAddsValue()
        {
            await this.cache.GetOrAddAsync(1, k => Task.FromResult(k));

            this.cache.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(1);
        }

        [Fact]
        public void WhenCacheContainsValuesTrim1RemovesColdestValue()
        {
            this.cache.AddOrUpdate(0, 0);
            this.cache.AddOrUpdate(1, 1);
            this.cache.AddOrUpdate(2, 2);

            this.cache.Trim(1);

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

        private void OnItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            this.removedItems.Add(e);
        }
    }
}
