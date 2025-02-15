using System;
using BitFaster.Caching.Buffers;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Buffers
{
    public class MpscBoundedBufferTests
    {
        private readonly MpscBoundedBuffer<string> buffer = new MpscBoundedBuffer<string>(10);

        [Fact]
        public void WhenSizeIsLessThan1CtorThrows()
        {
            Action constructor = () => { var x = new MpscBoundedBuffer<string>(-1); };

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
            buffer.TryAdd("1");
            buffer.Count.ShouldBe(1);
        }

        [Fact]
        public void WhenBufferHas15ItemCountIs15()
        {
            buffer.TryAdd("1").ShouldBe(BufferStatus.Success);
            buffer.TryTake(out var _).ShouldBe(BufferStatus.Success);

            for (var i = 0; i < 15; i++)
            {
                buffer.TryAdd("0").ShouldBe(BufferStatus.Success);
            }

            // head = 1, tail = 0 : head > tail
            buffer.Count.ShouldBe(15);
        }

        [Fact]
        public void WhenBufferIsFullTryAddIsFalse()
        {
            for (var i = 0; i < 16; i++)
            {
                buffer.TryAdd(i.ToString()).ShouldBe(BufferStatus.Success);
            }

            buffer.TryAdd("666").ShouldBe(BufferStatus.Full);
        }

        [Fact]
        public void WhenBufferIsEmptyTryTakeIsFalse()
        {
            buffer.TryTake(out var _).ShouldBe(BufferStatus.Empty);
        }

        [Fact]
        public void WhenItemAddedItCanBeTaken()
        {
            buffer.TryAdd("123").ShouldBe(BufferStatus.Success);
            buffer.TryTake(out var item).ShouldBe(BufferStatus.Success);
            item.ShouldBe("123");
        }

        [Fact]
        public void WhenItemsAreAddedClearRemovesItems()
        {
            buffer.TryAdd("1");
            buffer.TryAdd("2");

            buffer.Count.ShouldBe(2);

            buffer.Clear();

            buffer.Count.ShouldBe(0);
            buffer.TryTake(out var _).ShouldBe(BufferStatus.Empty);
        }

        [Fact]
        public void WhenBufferEmptyDrainReturnsZero()
        {
            var outputBuffer = new string[16];
            var output = new ArraySegment<string>(outputBuffer);

            buffer.DrainTo(output).ShouldBe(0);
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenBufferContainsItemsDrainArrayTakesItems()
        {
            buffer.TryAdd("1");
            buffer.TryAdd("2");
            buffer.TryAdd("3");

            var outputBuffer = new string[16];

            buffer.DrainTo(outputBuffer.AsSpan()).ShouldBe(3);

            outputBuffer[0].ShouldBe("1");
            outputBuffer[1].ShouldBe("2");
            outputBuffer[2].ShouldBe("3");
        }
#endif

        [Fact]
        public void WhenBufferContainsItemsDrainSegmentTakesItems()
        {
            buffer.TryAdd("1");
            buffer.TryAdd("2");
            buffer.TryAdd("3");

            var outputBuffer = new string[16];
            var output = new ArraySegment<string>(outputBuffer);

            buffer.DrainTo(output).ShouldBe(3);

            outputBuffer[0].ShouldBe("1");
            outputBuffer[1].ShouldBe("2");
            outputBuffer[2].ShouldBe("3");
        }

        [Fact]
        public void WhenSegmentUsesOffsetItemsDrainedToOffset()
        {
            buffer.TryAdd("1");
            buffer.TryAdd("2");
            buffer.TryAdd("3");

            var outputBuffer = new string[16];
            var output = new ArraySegment<string>(outputBuffer, 6, 10);

            buffer.DrainTo(output).ShouldBe(3);

            outputBuffer[6].ShouldBe("1");
            outputBuffer[7].ShouldBe("2");
            outputBuffer[8].ShouldBe("3");
        }
    }
}
