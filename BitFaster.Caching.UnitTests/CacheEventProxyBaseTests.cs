using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public CacheEventProxyBaseTests()
        {
            this.testCacheEvents = new TestCacheEvents<int, int>();
            this.eventProxy = new EventProxy<int, int>(this.testCacheEvents);
        }

        [Fact]
        public void WhenEventHandlerIsRegisteredItIsFired()
        {
            this.eventProxy.ItemRemoved += OnItemRemoved;

            this.testCacheEvents.Fire(1, new AtomicFactory<int, int>(1), ItemRemovedReason.Removed);

            this.removedItems.First().Key.Should().Be(1);
        }

        [Fact]
        public void WhenEventHandlerIsAddedThenRemovedItIsNotFired()
        {
            this.eventProxy.ItemRemoved += OnItemRemoved;
            this.eventProxy.ItemRemoved -= OnItemRemoved;

            this.testCacheEvents.Fire(1, new AtomicFactory<int, int>(1), ItemRemovedReason.Removed);

            this.removedItems.Count.Should().Be(0);
        }

        [Fact]
        public void WhenTwoEventHandlersAddedThenOneRemovedEventIsFired()
        {
            this.eventProxy.ItemRemoved += OnItemRemoved;
            this.eventProxy.ItemRemoved += OnItemRemovedThrow;
            this.eventProxy.ItemRemoved -= OnItemRemovedThrow;

            this.testCacheEvents.Fire(1, new AtomicFactory<int, int>(1), ItemRemovedReason.Removed);

            this.removedItems.First().Key.Should().Be(1);
        }

        private void OnItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            this.removedItems.Add(e);
        }

        private void OnItemRemovedThrow(object sender, ItemRemovedEventArgs<int, int> e)
        {
            throw new Exception("Should never happen");
        }

        private class TestCacheEvents<K, V> : ICacheEvents<K, AtomicFactory<K, V>>
        {
            public event EventHandler<ItemRemovedEventArgs<K, AtomicFactory<K, V>>> ItemRemoved;

            public void Fire(K key, AtomicFactory<K, V> value, ItemRemovedReason reason)
            {
                ItemRemoved?.Invoke(this, new ItemRemovedEventArgs<K, AtomicFactory<K, V>>(key, value, reason));
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
        }
    }
}
