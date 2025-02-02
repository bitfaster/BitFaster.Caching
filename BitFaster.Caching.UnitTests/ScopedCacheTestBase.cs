using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
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
            this.cache.Policy.Eviction.Value.Capacity.ShouldBe(capacity);
        }

        [Fact]
        public void WhenItemIsAddedCountIsCorrect()
        {
            this.cache.Count.ShouldBe(0);

            this.cache.AddOrUpdate(1, new Disposable());

            this.cache.Count.ShouldBe(1);
        }

        [Fact]
        public void WhenItemIsAddedThenLookedUpMetricsAreCorrect()
        {
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.ScopedTryGet(1, out var lifetime);

            this.cache.Metrics.Value.Misses.ShouldBe(0);
            this.cache.Metrics.Value.Hits.ShouldBe(1);
        }

        [Fact]
        public void WhenRemovedEventHandlerIsRegisteredItIsFired()
        {
            this.cache.Events.Value.ItemRemoved += OnItemRemoved;

            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.TryRemove(1);

            this.removedItems.First().Key.ShouldBe(1);
        }

// backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenUpdatedEventHandlerIsRegisteredItIsFired()
        {
            this.cache.Events.Value.ItemUpdated += OnItemUpdated;

            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.AddOrUpdate(1, new Disposable());

            this.updatedItems.First().Key.ShouldBe(1);
        }
#endif

        [Fact]
        public void WhenKeyDoesNotExistAddOrUpdateAddsNewItem()
        {
            var d = new Disposable();
            this.cache.AddOrUpdate(1, d);

            this.cache.ScopedTryGet(1, out var lifetime).ShouldBeTrue();
            lifetime.Value.ShouldBe(d);
        }

// backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenKeyDoesNotExistGetOrAddArgAddsValueWithArg()
        {
            this.cache.ScopedGetOrAdd(
                1,
                (k, a) => new Scoped<Disposable>(new Disposable(a)),
                2);

            this.cache.ScopedTryGet(1, out var lifetime).ShouldBeTrue();
            lifetime.Value.State.ShouldBe(2);
        }
#endif

        [Fact]
        public void WhenKeyExistsAddOrUpdateUpdatesExistingItem()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();
            this.cache.AddOrUpdate(1, d1);
            this.cache.AddOrUpdate(1, d2);

            this.cache.ScopedTryGet(1, out var lifetime).ShouldBeTrue();
            lifetime.Value.ShouldBe(d2);
        }

        [Fact]
        public void WhenItemUpdatedOldValueIsAliveUntilLifetimeCompletes()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();

            // start a lifetime on 1
            this.cache.AddOrUpdate(1, d1);
            this.cache.ScopedTryGet(1, out var lifetime1).ShouldBeTrue();

            using (lifetime1)
            {
                // replace 1
                this.cache.AddOrUpdate(1, d2);

                // cache reflects replacement
                this.cache.ScopedTryGet(1, out var lifetime2).ShouldBeTrue();
                lifetime2.Value.ShouldBe(d2);

                d1.IsDisposed.ShouldBeFalse();
            }

            d1.IsDisposed.ShouldBeTrue();
        }

        [Fact]
        public void WhenClearedItemsAreDisposed()
        {
            var d = new Disposable();
            this.cache.AddOrUpdate(1, d);

            this.cache.Clear();

            d.IsDisposed.ShouldBeTrue();
        }

        [Fact]
        public void WhenItemExistsTryGetReturnsLifetime()
        {
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.ScopedTryGet(1, out var lifetime).ShouldBeTrue();

            lifetime.ShouldNotBeNull();
        }

        [Fact]
        public void WhenItemDoesNotExistTryGetReturnsFalse()
        {
            this.cache.ScopedTryGet(1, out var lifetime).ShouldBeFalse();
        }

        [Fact]
        public void WhenCacheContainsValuesTrim1RemovesColdestValue()
        {
            this.cache.AddOrUpdate(0, new Disposable());
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.AddOrUpdate(2, new Disposable());

            this.cache.Policy.Eviction.Value.Trim(1);

            this.cache.ScopedTryGet(0, out var lifetime).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryRemoveReturnsFalse()
        {
            this.cache.TryRemove(1).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryRemoveReturnsTrue()
        {
            this.cache.AddOrUpdate(1, new Disposable());
            this.cache.TryRemove(1).ShouldBeTrue();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryUpdateReturnsFalse()
        {
            this.cache.TryUpdate(1, new Disposable()).ShouldBeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryUpdateReturnsTrue()
        {
            this.cache.AddOrUpdate(1, new Disposable());

            this.cache.TryUpdate(1, new Disposable()).ShouldBeTrue();
        }

        [Fact]
        public void WhenItemsAddedKeysContainsTheKeys()
        {
            cache.Count.ShouldBe(0);
            cache.AddOrUpdate(1, new Disposable());
            cache.AddOrUpdate(2, new Disposable());
            cache.Keys.ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void WhenItemsAddedGenericEnumerateContainsKvps()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();

            cache.Count.ShouldBe(0);
            cache.AddOrUpdate(1, d1);
            cache.AddOrUpdate(2, d2);
            cache
                .Select(kvp => new KeyValuePair<int, Disposable>(kvp.Key, kvp.Value.CreateLifetime().Value))
                .ShouldBe(new[] { new KeyValuePair<int, Disposable>(1, d1), new KeyValuePair<int, Disposable>(2, d2) });
        }

        [Fact]
        public void WhenItemsAddedEnumerateContainsKvps()
        {
            var d1 = new Disposable();
            var d2 = new Disposable();

            cache.Count.ShouldBe(0);
            cache.AddOrUpdate(1, d1);
            cache.AddOrUpdate(2, d2);

            var enumerable = (IEnumerable)cache;

            var list = new List<KeyValuePair<int, Disposable>>();

            foreach (var i in enumerable)
            {
                var kvp = (KeyValuePair<int, Scoped<Disposable>>)i;
                list.Add(new KeyValuePair<int, Disposable>(kvp.Key, kvp.Value.CreateLifetime().Value));
            }

            list.ShouldBe(new[] { new KeyValuePair<int, Disposable>(1, d1), new KeyValuePair<int, Disposable>(2, d2) });
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
