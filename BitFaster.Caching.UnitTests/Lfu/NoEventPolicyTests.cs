using System;
using System.Collections.Generic;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class NoEventPolicyTests
    {
        private NoEventPolicy<int, int> noEventPolicy = default;

        [Fact]
        public void OnItemRemovedDoesNothing()
        {
            Action act = () => noEventPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            act.Should().NotThrow();
        }

        [Fact]
        public void OnItemUpdatedDoesNothing()
        {
            Action act = () => noEventPolicy.OnItemUpdated(1, 2, 3);

            act.Should().NotThrow();
        }

        [Fact]
        public void SetEventSourceDoesNothing()
        {
            Action act = () => noEventPolicy.SetEventSource(this);

            act.Should().NotThrow();
        }

        [Fact]
        public void ItemRemovedEventCanBeSubscribedWithoutEffect()
        {
            List<ItemRemovedEventArgs<int, int>> eventList = new();

            noEventPolicy.ItemRemoved += (source, args) => eventList.Add(args);

            noEventPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            eventList.Should().BeEmpty();
        }

        [Fact]
        public void ItemUpdatedEventCanBeSubscribedWithoutEffect()
        {
            List<ItemUpdatedEventArgs<int, int>> eventList = new();

            noEventPolicy.ItemUpdated += (source, args) => eventList.Add(args);

            noEventPolicy.OnItemUpdated(1, 2, 3);

            eventList.Should().BeEmpty();
        }

        [Fact]
        public void ItemRemovedEventCanBeUnsubscribedWithoutEffect()
        {
            EventHandler<ItemRemovedEventArgs<int, int>> handler = (source, args) => { };

            Action subscribe = () => noEventPolicy.ItemRemoved += handler;
            Action unsubscribe = () => noEventPolicy.ItemRemoved -= handler;

            subscribe.Should().NotThrow();
            unsubscribe.Should().NotThrow();
        }

        [Fact]
        public void ItemUpdatedEventCanBeUnsubscribedWithoutEffect()
        {
            EventHandler<ItemUpdatedEventArgs<int, int>> handler = (source, args) => { };

            Action subscribe = () => noEventPolicy.ItemUpdated += handler;
            Action unsubscribe = () => noEventPolicy.ItemUpdated -= handler;

            subscribe.Should().NotThrow();
            unsubscribe.Should().NotThrow();
        }
    }
}
