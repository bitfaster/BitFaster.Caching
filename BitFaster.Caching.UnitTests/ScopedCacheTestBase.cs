
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public abstract class ScopedCacheTestBase
    {
        protected const int capacity = 6;
        protected readonly IScopedCache<int, Disposable> cache;

        protected List<ItemRemovedEventArgs<int, Scoped<Disposable>>> removedItems = new();
        protected List<ItemUpdatedEventArgs<int, Scoped<Disposable>>> updatedItems = new();

        protected ScopedCacheTestBase(IScopedCache<int, Disposable> cache)
        {
            this.cache = cache;
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

            this.cache.AddOrUpdate(1, new Disposable());

            this.cache.Count.Should().Be(1);
        }

        [Fact]
        public void WhenItemIsAddedThenLookedUpMetricsAreCorrect()
        {
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.ScopedTryGet(1, out var lifetime);

            this.cache.Metrics.Value.Misses.Should().Be(0);
            this.cache.Metrics.Value.Hits.Should().Be(1);
        }

        [Fact]
        public void WhenRemovedEventHandlerIsRegisteredItIsFired()
        {
            this.cache.Events.Value.ItemRemoved += OnItemRemoved;

            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.TryRemove(1);

            this.removedItems.First().Key.Should().Be(1);
        }

        [Fact]
        public void WhenUpdatedEventHandlerIsRegisteredItIsFired()
        {
            this.cache.Events.Value.ItemUpdated += OnItemUpdated;

            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.AddOrUpdate(1, new Disposable());

            this.updatedItems.First().Key.Should().Be(1);
        }

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateAddsNewItem()
        {
            var d = new Disposable();
            this.cache.AddOrUpdate(1, d);

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();
            lifetime.Value.Should().Be(d);
        }

        [Fact]
        public void WhenKeyExistsAddOrUpdateUpdatesExistingItem()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();
            this.cache.AddOrUpdate(1, d1);
            this.cache.AddOrUpdate(1, d2);

            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();
            lifetime.Value.Should().Be(d2);
        }

        [Fact]
        public void WhenItemUpdatedOldValueIsAliveUntilLifetimeCompletes()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();

            // start a lifetime on 1
            this.cache.AddOrUpdate(1, d1);
            this.cache.ScopedTryGet(1, out var lifetime1).Should().BeTrue();

            using (lifetime1)
            {
                // replace 1
                this.cache.AddOrUpdate(1, d2);

                // cache reflects replacement
                this.cache.ScopedTryGet(1, out var lifetime2).Should().BeTrue();
                lifetime2.Value.Should().Be(d2);

                d1.IsDisposed.Should().BeFalse();
            }

            d1.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenClearedItemsAreDisposed()
        {
            var d = new Disposable();
            this.cache.AddOrUpdate(1, d);

            this.cache.Clear();

            d.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void WhenItemExistsTryGetReturnsLifetime()
        {
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.ScopedTryGet(1, out var lifetime).Should().BeTrue();

            lifetime.Should().NotBeNull();
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetReturnsFalse()
        {
            this.cache.ScopedTryGet(1, out var lifetime).Should().BeFalse();
        }

        [Fact]
        public void WhenCacheContainsValuesTrim1RemovesColdestValue()
        {
            this.cache.AddOrUpdate(0, new Disposable());
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.AddOrUpdate(2, new Disposable());

            this.cache.Policy.Eviction.Value.Trim(1);

            this.cache.ScopedTryGet(0, out var lifetime).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveReturnsFalse()
        {
            this.cache.TryRemove(1).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryRemoveReturnsTrue()
        {
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.TryRemove(1).Should().BeTrue();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryUpdateReturnsFalse()
        {
            this.cache.TryUpdate(1, new Disposable()).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryUpdateReturnsTrue()
        {
            this.cache.AddOrUpdate(1, new Disposable());

            this.cache.TryUpdate(1, new Disposable()).Should().BeTrue();
        }

        [Fact]
        public void WhenItemsAddedKeysContainsTheKeys()
        {
            cache.Count.Should().Be(0);
            cache.AddOrUpdate(1, new Disposable());
            cache.AddOrUpdate(2, new Disposable());
            cache.Keys.Should().BeEquivalentTo(new[] { 1, 2 });
        }

        [Fact]
        public void WhenItemsAddedGenericEnumerateContainsKvps()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();

            cache.Count.Should().Be(0);
            cache.AddOrUpdate(1, d1);
            cache.AddOrUpdate(2, d2);
            cache
                .Select(kvp => new KeyValuePair<int, Disposable>(kvp.Key, kvp.Value.CreateLifetime().Value))
                .Should().BeEquivalentTo(new[] { new KeyValuePair<int, Disposable>(1, d1), new KeyValuePair<int, Disposable>(2, d2) });
        }

        [Fact]
        public void WhenItemsAddedEnumerateContainsKvps()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();

            cache.Count.Should().Be(0);
            cache.AddOrUpdate(1, d1);
            cache.AddOrUpdate(2, d2);

            var enumerable = (IEnumerable)cache;

            var list = new List<KeyValuePair<int, Disposable>>();

            foreach (var i in enumerable)
            {
                var kvp = (KeyValuePair<int, Scoped<Disposable>>)i;
                list.Add(new KeyValuePair<int, Disposable>(kvp.Key, kvp.Value.CreateLifetime().Value));
            }

            list.Should().BeEquivalentTo(new[] { new KeyValuePair<int, Disposable>(1, d1), new KeyValuePair<int, Disposable>(2, d2) });
        }

        protected void OnItemRemoved(object sender, ItemRemovedEventArgs<int, Scoped<Disposable>> e)
        {
            this.removedItems.Add(e);
        }

        protected void OnItemUpdated(object sender, ItemUpdatedEventArgs<int, Scoped<Disposable>> e)
        {
            this.updatedItems.Add(e);
        }
    }
}
