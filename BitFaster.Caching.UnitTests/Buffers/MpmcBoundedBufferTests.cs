using System;
using BitFaster.Caching.Buffers;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Buffers
{
    public class MpmcBoundedBufferTests
    {
        private readonly MpmcBoundedBuffer<int> buffer = new MpmcBoundedBuffer<int>(10);

        [Fact]
        public void WhenSizeIsLessThan1CtorThrows()
        {
            Action constructor = () => { var x = new MpmcBoundedBuffer<int>(-1); };

            constructor.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void SizeIsPowerOfTwo()
        {
            buffer.Capacity.ShouldBe(16);
        }

        [Fact]
        public void WhenBufferIsEmptyCountIsZero()
        {
            buffer.Count.ShouldBe(0);
        }

        [Fact]
        public void WhenBufferHasOneItemCountIsOne()
        {
            // head < tail
            buffer.TryAdd(1);
            buffer.Count.ShouldBe(1);
        }

        [Fact]
        public void WhenBufferHas15ItemCountIs15()
        {
            buffer.TryAdd(0).ShouldBe(BufferStatus.Success);
            buffer.TryTake(out var _).ShouldBe(BufferStatus.Success);

            for (var i = 0; i < 15; i++)
            {
                buffer.TryAdd(0).ShouldBe(BufferStatus.Success);
            }

            // head = 1, tail = 0 : head > tail
            buffer.Count.ShouldBe(15);
        }

        [Fact]
        public void WhenBufferIsFullTryAddIsFalse()
        {
            for (var i = 0; i < 16; i++)
            {
                buffer.TryAdd(i).ShouldBe(BufferStatus.Success);
            }

            buffer.TryAdd(666).ShouldBe(BufferStatus.Full);
        }

        [Fact]
        public void WhenBufferIsEmptyTryTakeIsFalse()
        {
            buffer.TryTake(out var _).ShouldBe(BufferStatus.Empty);
        }

        [Fact]
        public void WhenItemAddedItCanBeTaken()
        {
            buffer.TryAdd(123).ShouldBe(BufferStatus.Success);
            buffer.TryTake(out var item).ShouldBe(BufferStatus.Success);
            item.ShouldBe(123);
        }

        [Fact]
        public void WhenItemsAreAddedClearRemovesItems()
        {
            buffer.TryAdd(1);
            buffer.TryAdd(2);

            buffer.Count.ShouldBe(2);

            buffer.Clear();

            buffer.Count.ShouldBe(0);
            buffer.TryTake(out var _).ShouldBe(BufferStatus.Empty);
        }
    }
}
