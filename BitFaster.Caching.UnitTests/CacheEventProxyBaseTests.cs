using System;
using System.Collections.Generic;
using System.Linq;
using BitFaster.Caching.Atomic;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class CacheEventProxyBaseTests
    {
        private TestCacheEvents<int, int> testCacheEvents;
        private EventProxy<int, int> eventProxy;

        private List<ItemRemovedEventArgs<int, int>> removedItems = new();
        private List<ItemUpdatedEventArgs<int, int>> updatedItems = new();

        public CacheEventProxyBaseTests()
        {
            this.testCacheEvents = new TestCacheEvents<int, int>();
            this.eventProxy = new EventProxy<int, int>(this.testCacheEvents);
        }

        [Fact]
        public void WheRemovedEventHandlerIsRegisteredItIsFired()
        {
            this.eventProxy.ItemRemoved += OnItemRemoved;

            this.testCacheEvents.FireRemoved(1, new AtomicFactory<int, int>(1), ItemRemovedReason.Removed);

            this.removedItems.First().Key.Should().Be(1);
        }

        [Fact]
        public void WhenRemovedEventHandlerIsAddedThenRemovedItIsNotFired()
        {
            this.eventProxy.ItemRemoved += OnItemRemoved;
            this.eventProxy.ItemRemoved -= OnItemRemoved;

            this.testCacheEvents.FireRemoved(1, new AtomicFactory<int, int>(1), ItemRemovedReason.Removed);

            this.removedItems.Count.Should().Be(0);
        }

        [Fact]
        public void WhenTwoRemovedEventHandlersAddedThenOneRemovedEventIsFired()
        {
            this.eventProxy.ItemRemoved += OnItemRemoved;
            this.eventProxy.ItemRemoved += OnItemRemovedThrow;
            this.eventProxy.ItemRemoved -= OnItemRemovedThrow;

            this.testCacheEvents.FireRemoved(1, new AtomicFactory<int, int>(1), ItemRemovedReason.Removed);

            this.removedItems.First().Key.Should().Be(1);
        }

// backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WheUpdatedEventHandlerIsRegisteredItIsFired()
        {
            this.eventProxy.ItemUpdated += OnItemUpdated;

            this.testCacheEvents.FireUpdated(1, new AtomicFactory<int, int>(2), new AtomicFactory<int, int>(3));

            this.updatedItems.First().Key.Should().Be(1);
            this.updatedItems.First().OldValue.Should().Be(2);
            this.updatedItems.First().NewValue.Should().Be(3);
        }

        [Fact]
        public void WhenUpdatedEventHandlerIsAddedThenRemovedItIsNotFired()
        {
            this.eventProxy.ItemUpdated += OnItemUpdated;
            this.eventProxy.ItemUpdated -= OnItemUpdated;

            this.testCacheEvents.FireUpdated(1, new AtomicFactory<int, int>(2), new AtomicFactory<int, int>(3));

            this.updatedItems.Count.Should().Be(0);
        }

        [Fact]
        public void WhenTwoUpdatedEventHandlersAddedThenOneRemovedEventIsFired()
        {
            this.eventProxy.ItemUpdated += OnItemUpdated;
            this.eventProxy.ItemUpdated += OnItemUpdatedThrow;
            this.eventProxy.ItemUpdated -= OnItemUpdatedThrow;

            this.testCacheEvents.FireUpdated(1, new AtomicFactory<int, int>(2), new AtomicFactory<int, int>(3));

            this.updatedItems.First().Key.Should().Be(1);
        }
#endif
        private void OnItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            this.removedItems.Add(e);
        }

        private void OnItemUpdated(object sender, ItemUpdatedEventArgs<int, int> e)
        {
            this.updatedItems.Add(e);
        }

        private void OnItemRemovedThrow(object sender, ItemRemovedEventArgs<int, int> e)
        {
            throw new Exception("Should never happen");
        }

        private void OnItemUpdatedThrow(object sender, ItemUpdatedEventArgs<int, int> e)
        {
            throw new Exception("Should never happen");
        }

        private class TestCacheEvents<K, V> : ICacheEvents<K, AtomicFactory<K, V>>
        {
            public event EventHandler<ItemRemovedEventArgs<K, AtomicFactory<K, V>>> ItemRemoved;
            public event EventHandler<ItemUpdatedEventArgs<K, AtomicFactory<K, V>>> ItemUpdated;

            public void FireRemoved(K key, AtomicFactory<K, V> value, ItemRemovedReason reason)
            {
                ItemRemoved?.Invoke(this, new ItemRemovedEventArgs<K, AtomicFactory<K, V>>(key, value, reason));
            }

            public void FireUpdated(K key, AtomicFactory<K, V> oldValue, AtomicFactory<K, V> newValue)
            {
                ItemUpdated?.Invoke(this, new ItemUpdatedEventArgs<K, AtomicFactory<K, V>>(key, oldValue, newValue));
            }
        }

        private class EventProxy<K, V> : CacheEventProxyBase<K, AtomicFactory<K, V>, V>
        {
            public EventProxy(ICacheEvents<K, AtomicFactory<K, V>> inner)
                : base(inner)
            {
            }

            protected override ItemRemovedEventArgs<K, V> TranslateOnRemoved(ItemRemovedEventArgs<K, AtomicFactory<K, V>> inner)
            {
                return new ItemRemovedEventArgs<K, V>(inner.Key, inner.Value.ValueIfCreated, inner.Reason);
            }

            protected override ItemUpdatedEventArgs<K, V> TranslateOnUpdated(ItemUpdatedEventArgs<K, AtomicFactory<K, V>> inner)
            {
                return new ItemUpdatedEventArgs<K, V>(inner.Key, inner.OldValue.ValueIfCreated, inner.NewValue.ValueIfCreated);
            }
        }

        // backcompat: remove (virtual method with default impl only needed for back compat)
        [Fact]
        public void WhenUpdatedEventHandlerIsRegisteredAndProxyUsesDefaultUpdateTranslateItIsFired()
        {
            var proxy = new EventProxyWithDefault<int, int>(this.testCacheEvents);

            proxy.ItemUpdated += OnItemUpdated;

            this.testCacheEvents.FireUpdated(1, new AtomicFactory<int, int>(2), new AtomicFactory<int, int>(3));

#if NETCOREAPP3_0_OR_GREATER
            this.updatedItems.First().Key.Should().Be(1);
#else
            this.updatedItems.Should().BeEmpty();
#endif
        }

        // backcompat: remove (class uses default TranslateOnUpdated method)
        private class EventProxyWithDefault<K, V> : CacheEventProxyBase<K, AtomicFactory<K, V>, V>
        {
            public EventProxyWithDefault(ICacheEvents<K, AtomicFactory<K, V>> inner)
                : base(inner)
            {
            }

            protected override ItemRemovedEventArgs<K, V> TranslateOnRemoved(ItemRemovedEventArgs<K, AtomicFactory<K, V>> inner)
            {
                return new ItemRemovedEventArgs<K, V>(inner.Key, inner.Value.ValueIfCreated, inner.Reason);
            }
        }
    }
}
