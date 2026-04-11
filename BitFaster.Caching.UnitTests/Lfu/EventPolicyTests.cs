using System;
using System.Collections.Generic;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class EventPolicyTests
    {
        private EventPolicy<int, int> eventPolicy = default;

        public EventPolicyTests()
        {
            eventPolicy.SetEventSource(this);
        }

        [Fact]
        public void OnItemRemovedInvokesEvent()
        {
            List<ItemRemovedEventArgs<int, int>> eventList = new();

            eventPolicy.ItemRemoved += (source, args) => eventList.Add(args);

            eventPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            eventList.Should().HaveCount(1);
            eventList[0].Key.Should().Be(1);
            eventList[0].Value.Should().Be(2);
            eventList[0].Reason.Should().Be(ItemRemovedReason.Evicted);
        }

        [Fact]
        public void OnItemUpdatedInvokesEvent()
        {
            List<ItemUpdatedEventArgs<int, int>> eventList = new();

            eventPolicy.ItemUpdated += (source, args) => eventList.Add(args);

            eventPolicy.OnItemUpdated(1, 2, 3);

            eventList.Should().HaveCount(1);
            eventList[0].Key.Should().Be(1);
            eventList[0].OldValue.Should().Be(2);
            eventList[0].NewValue.Should().Be(3);
        }

        [Fact]
        public void EventSourceIsSetItemRemovedEventUsesSource()
        {
            List<object> eventSourceList = new();

            eventPolicy.SetEventSource(this);

            eventPolicy.ItemRemoved += (source, args) => eventSourceList.Add(source);

            eventPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            eventSourceList.Should().HaveCount(1);
            eventSourceList[0].Should().Be(this);
        }

        [Fact]
        public void EventSourceIsSetItemUpdatedEventUsesSource()
        {
            List<object> eventSourceList = new();

            eventPolicy.SetEventSource(this);

            eventPolicy.ItemUpdated += (source, args) => eventSourceList.Add(source);

            eventPolicy.OnItemUpdated(1, 2, 3);

            eventSourceList.Should().HaveCount(1);
            eventSourceList[0].Should().Be(this);
        }

        [Fact]
        public void MultipleItemRemovedSubscribersAllInvoked()
        {
            int invocationCount = 0;

            eventPolicy.ItemRemoved += (source, args) => invocationCount++;
            eventPolicy.ItemRemoved += (source, args) => invocationCount++;
            eventPolicy.ItemRemoved += (source, args) => invocationCount++;

            eventPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            invocationCount.Should().Be(3);
        }

        [Fact]
        public void MultipleItemUpdatedSubscribersAllInvoked()
        {
            int invocationCount = 0;

            eventPolicy.ItemUpdated += (source, args) => invocationCount++;
            eventPolicy.ItemUpdated += (source, args) => invocationCount++;
            eventPolicy.ItemUpdated += (source, args) => invocationCount++;

            eventPolicy.OnItemUpdated(1, 2, 3);

            invocationCount.Should().Be(3);
        }

        [Fact]
        public void ItemRemovedEventCanBeUnsubscribed()
        {
            int invocationCount = 0;

            EventHandler<ItemRemovedEventArgs<int, int>> handler = (source, args) => invocationCount++;

            eventPolicy.ItemRemoved += handler;
            eventPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            invocationCount.Should().Be(1);

            eventPolicy.ItemRemoved -= handler;
            eventPolicy.OnItemRemoved(3, 4, ItemRemovedReason.Evicted);

            invocationCount.Should().Be(1);
        }

        [Fact]
        public void ItemUpdatedEventCanBeUnsubscribed()
        {
            int invocationCount = 0;

            EventHandler<ItemUpdatedEventArgs<int, int>> handler = (source, args) => invocationCount++;

            eventPolicy.ItemUpdated += handler;
            eventPolicy.OnItemUpdated(1, 2, 3);

            invocationCount.Should().Be(1);

            eventPolicy.ItemUpdated -= handler;
            eventPolicy.OnItemUpdated(4, 5, 6);

            invocationCount.Should().Be(1);
        }

        [Fact]
        public void OnItemRemovedWithoutSubscribersDoesNotThrow()
        {
            Action act = () => eventPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);

            act.Should().NotThrow();
        }

        [Fact]
        public void OnItemUpdatedWithoutSubscribersDoesNotThrow()
        {
            Action act = () => eventPolicy.OnItemUpdated(1, 2, 3);

            act.Should().NotThrow();
        }

        [Fact]
        public void MultipleOnItemRemovedCallsInvokeMultipleEvents()
        {
            List<ItemRemovedEventArgs<int, int>> eventList = new();

            eventPolicy.ItemRemoved += (source, args) => eventList.Add(args);

            eventPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);
            eventPolicy.OnItemRemoved(3, 4, ItemRemovedReason.Removed);
            eventPolicy.OnItemRemoved(5, 6, ItemRemovedReason.Evicted);

            eventList.Should().HaveCount(3);
            eventList[0].Key.Should().Be(1);
            eventList[1].Key.Should().Be(3);
            eventList[2].Key.Should().Be(5);
        }

        [Fact]
        public void MultipleOnItemUpdatedCallsInvokeMultipleEvents()
        {
            List<ItemUpdatedEventArgs<int, int>> eventList = new();

            eventPolicy.ItemUpdated += (source, args) => eventList.Add(args);

            eventPolicy.OnItemUpdated(1, 2, 3);
            eventPolicy.OnItemUpdated(4, 5, 6);
            eventPolicy.OnItemUpdated(7, 8, 9);

            eventList.Should().HaveCount(3);
            eventList[0].Key.Should().Be(1);
            eventList[1].Key.Should().Be(4);
            eventList[2].Key.Should().Be(7);
        }

        [Fact]
        public void ItemRemovedAndItemUpdatedEventsAreIndependent()
        {
            List<ItemRemovedEventArgs<int, int>> removedEventList = new();
            List<ItemUpdatedEventArgs<int, int>> updatedEventList = new();

            eventPolicy.ItemRemoved += (source, args) => removedEventList.Add(args);
            eventPolicy.ItemUpdated += (source, args) => updatedEventList.Add(args);

            eventPolicy.OnItemRemoved(1, 2, ItemRemovedReason.Evicted);
            eventPolicy.OnItemUpdated(3, 4, 5);

            removedEventList.Should().HaveCount(1);
            updatedEventList.Should().HaveCount(1);
        }
    }
}
