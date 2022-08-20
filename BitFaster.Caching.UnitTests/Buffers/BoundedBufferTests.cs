using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Buffers
{
    public class BoundedBufferTests
    {
        private readonly BoundedBuffer<int> buffer = new BoundedBuffer<int>(10);

        [Fact]
        public void WhenSizeIsLessThan1CtorThrows()
        {
            Action constructor = () => { var x = new BoundedBuffer<int>(-1); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void SizeIsPowerOfTwo()
        {
            buffer.Capacity.Should().Be(16);
        }

        [Fact]
        public void WhenBufferIsEmptyCountIsZero()
        {
            buffer.Count.Should().Be(0);
        }

        [Fact]
        public void WhenBufferHasOneItemCountIsOne()
        {
            // head < tail
            buffer.TryAdd(1);
            buffer.Count.Should().Be(1);
        }

        [Fact]
        public void WhenBufferHas15ItemCountIs15()
        {
            buffer.TryAdd(0).Should().Be(BufferStatus.Success);
            buffer.TryTake(out var _).Should().Be(BufferStatus.Success);

            for (var i = 0; i < 15; i++)
            {
                buffer.TryAdd(0).Should().Be(BufferStatus.Success);
            }

            // head = 1, tail = 0 : head > tail
            buffer.Count.Should().Be(15);
        }

        [Fact]
        public void WhenBufferIsFullTryAddIsFalse()
        {
            for (var i = 0; i < 16; i++)
            {
                buffer.TryAdd(i).Should().Be(BufferStatus.Success);
            }

            buffer.TryAdd(666).Should().Be(BufferStatus.Full);
        }

        [Fact]
        public void WhenBufferIsEmptyTryTakeIsFalse()
        {
            buffer.TryTake(out var _).Should().Be(BufferStatus.Empty);
        }

        [Fact]
        public void WhenItemAddedItCanBeTaken()
        {
            buffer.TryAdd(123).Should().Be(BufferStatus.Success);
            buffer.TryTake(out var item).Should().Be(BufferStatus.Success);
            item.Should().Be(123);
        }

        [Fact]
        public void WhenItemsAreAddedClearRemovesItems()
        {
            buffer.TryAdd(1);
            buffer.TryAdd(2);

            buffer.Count.Should().Be(2);

            buffer.Clear();

            buffer.Count.Should().Be(0);
            buffer.TryTake(out var _).Should().Be(BufferStatus.Empty);
        }
    }
}
