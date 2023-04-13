using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;
using FluentAssertions;
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
            buffer.TryAdd("1");
            buffer.Count.Should().Be(1);
        }

        [Fact]
        public void WhenBufferHas15ItemCountIs15()
        {
            buffer.TryAdd("1").Should().Be(BufferStatus.Success);
            buffer.TryTake(out var _).Should().Be(BufferStatus.Success);

            for (var i = 0; i < 15; i++)
            {
                buffer.TryAdd("0").Should().Be(BufferStatus.Success);
            }

            // head = 1, tail = 0 : head > tail
            buffer.Count.Should().Be(15);
        }

        [Fact]
        public void WhenBufferIsFullTryAddIsFalse()
        {
            for (var i = 0; i < 16; i++)
            {
                buffer.TryAdd(i.ToString()).Should().Be(BufferStatus.Success);
            }

            buffer.TryAdd("666").Should().Be(BufferStatus.Full);
        }

        [Fact]
        public void WhenBufferIsEmptyTryTakeIsFalse()
        {
            buffer.TryTake(out var _).Should().Be(BufferStatus.Empty);
        }

        [Fact]
        public void WhenItemAddedItCanBeTaken()
        {
            buffer.TryAdd("123").Should().Be(BufferStatus.Success);
            buffer.TryTake(out var item).Should().Be(BufferStatus.Success);
            item.Should().Be("123");
        }

        [Fact]
        public void WhenItemsAreAddedClearRemovesItems()
        {
            buffer.TryAdd("1");
            buffer.TryAdd("2");

            buffer.Count.Should().Be(2);

            buffer.Clear();

            buffer.Count.Should().Be(0);
            buffer.TryTake(out var _).Should().Be(BufferStatus.Empty);
        }

        [Fact]
        public void WhenBufferEmptyDrainReturnsZero()
        {
            var outputBuffer = new string[16];
            var output = new ArraySegment<string>(outputBuffer);

            buffer.DrainTo(output).Should().Be(0);
        }

        [Fact]
        public void WhenBufferContainsItemsDrainArrayTakesItems()
        {
            buffer.TryAdd("1");
            buffer.TryAdd("2");
            buffer.TryAdd("3");

            var outputBuffer = new string[16];

            buffer.DrainTo(outputBuffer).Should().Be(3);

            outputBuffer[0].Should().Be("1");
            outputBuffer[1].Should().Be("2");
            outputBuffer[2].Should().Be("3");
        }

        [Fact]
        public void WhenBufferContainsItemsDrainSegmentTakesItems()
        {
            buffer.TryAdd("1");
            buffer.TryAdd("2");
            buffer.TryAdd("3");

            var outputBuffer = new string[16];
            var output = new ArraySegment<string>(outputBuffer);

            buffer.DrainTo(output).Should().Be(3);

            outputBuffer[0].Should().Be("1");
            outputBuffer[1].Should().Be("2");
            outputBuffer[2].Should().Be("3");
        }

        [Fact]
        public void WhenSegmentUsesOffsetItemsDrainedToOffset()
        {
            buffer.TryAdd("1");
            buffer.TryAdd("2");
            buffer.TryAdd("3");

            var outputBuffer = new string[16];
            var output = new ArraySegment<string>(outputBuffer, 6, 10);

            buffer.DrainTo(output).Should().Be(3);

            outputBuffer[6].Should().Be("1");
            outputBuffer[7].Should().Be("2");
            outputBuffer[8].Should().Be("3");
        }

        [Fact]
        public async Task WhenAddIsContendedBufferCanBeFilled()
        {
            var buffer = new MpscBoundedBuffer<string>(1024);

            await Threaded.Run(4, () =>
            {
                while (buffer.TryAdd("hello") != BufferStatus.Full)
                { 
                }

                buffer.Count.Should().Be(1024);
            });
        }

        [Fact(Timeout = 5000)]
        public async Task WhileBufferIsFilledItemsCanBeTaken()
        {
            var buffer = new MpscBoundedBuffer<string>(1024);

            var fill = Threaded.Run(4, () =>
            {
                int count = 0;
                while (count < 256)
                {
                    if (buffer.TryAdd("hello") == BufferStatus.Success)
                    {
                        count++;
                    }
                }
            });

            int taken = 0;

            while (taken < 1024)
            {
                if (buffer.TryTake(out var _) == BufferStatus.Success) 
                {
                    taken++;
                }
            }

            await fill;
        }

        [Fact(Timeout = 5000)]
        public async Task WhileBufferIsFilledBufferCanBeDrained()
        {
            var buffer = new MpscBoundedBuffer<string>(1024);

            var fill = Threaded.Run(4, () =>
            {
                int count = 0;
                while (count < 256)
                {
                    if (buffer.TryAdd("hello") == BufferStatus.Success)
                    {
                        count++;
                    }
                }
            });

            int drained = 0;
            var drainBuffer = new ArraySegment<string>(new string[1024]);

            while (drained < 1024)
            {
                drained += buffer.DrainTo(drainBuffer);
            }

            await fill;
        }
    }
}
