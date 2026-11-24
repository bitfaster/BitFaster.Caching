using System;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class LfuNodeListTests
    {
        [Fact]
        public void AddFirstToNonEmptyListPreservesForwardAndBackwardLinks()
        {
            var list = new LfuNodeList<int, int>();
            var first = new LfuNode<int, int>(1, 1);
            var last = new LfuNode<int, int>(2, 2);
            list.AddLast(last);

            list.AddFirst(first);

            list.Should().Equal(first, last);
            first.Previous.Should().BeNull();
            last.Previous.Should().BeSameAs(first);
            list.Last.Should().BeSameAs(last);
        }

        [Fact]
        public void MoveToFrontMovesLastNodeWithoutChangingCount()
        {
            var list = new LfuNodeList<int, int>();
            var first = new LfuNode<int, int>(1, 1);
            var last = new LfuNode<int, int>(2, 2);
            list.AddLast(first);
            list.AddLast(last);

            list.MoveToFront(last);

            list.Should().Equal(last, first);
            list.Count.Should().Be(2);
            first.Previous.Should().BeSameAs(last);
        }

#if DEBUG
        [Fact]
        public void WhenEmptyRemoveFirstThrows()
        {
            var list = new LfuNodeList<int, int>();
            Action remove = () => { list.RemoveFirst(); };
            remove.Should().Throw<InvalidOperationException>();
        }
#endif

        [Fact]
        public void WhenPreviousNullLastReturnsNull()
        {
            var list = new LfuNodeList<int, int>();

            list.Last.Should().BeNull();
        }

        [Fact]
        public void WhenPreviousExistsLastReturnsPrevious()
        {
            var list = new LfuNodeList<int, int>();
            var node1 = new LfuNode<int, int>(1, 1);
            var node2 = new LfuNode<int, int>(2, 2);

            list.AddLast(node1);
            list.AddLast(node2);

            list.Last.Should().BeSameAs(node2);
        }

        [Fact]
        public void WhenPreviousExistsNodePreviousReturnsPrevious()
        {
            var list = new LfuNodeList<int, int>();
            var node1 = new LfuNode<int, int>(1, 1);
            var node2 = new LfuNode<int, int>(2, 2);

            list.AddLast(node1);
            list.AddLast(node2);

            node2.Previous.Should().BeSameAs(node1);
        }

        [Fact]
        public void WhenHeadNodePreviousReturnsNull()
        {
            var list = new LfuNodeList<int, int>();
            var node1 = new LfuNode<int, int>(1, 1);
            var node2 = new LfuNode<int, int>(2, 2);

            list.AddLast(node1);
            list.AddLast(node2);

            node1.Previous.Should().BeNull();
        }
    }
}
