using BitFaster.Caching.Buffers;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Buffers
{
    public class StripedMpscBufferTests
    {
        const int bufferSize = 16;
        const int stripeCount = 2;
        private readonly StripedMpscBuffer<string> buffer = new StripedMpscBuffer<string>(stripeCount, bufferSize);

        [Fact]
        public void CapacityReturnsCapacity()
        {
            buffer.Capacity.ShouldBe(32);
        }

        [Fact]
        public void CountReturnsCount()
        {
            buffer.Count.ShouldBe(0);

            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd(1.ToString()).ShouldBe(BufferStatus.Success);
                }
            }

            buffer.Count.ShouldBe(buffer.Capacity);
        }

        [Fact]
        public void WhenBufferIsFullTryAddReturnsFull()
        {
            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd(1.ToString()).ShouldBe(BufferStatus.Success);
                }
            }

            buffer.TryAdd("1").ShouldBe(BufferStatus.Full);
        }

        [Fact]
        public void WhenBufferIsEmptyDrainReturnsZero()
        {
            var array = new string[bufferSize];
            buffer.DrainTo(array).ShouldBe(0);
        }

        [Fact]
        public void WhenBufferIsFullDrainReturnsItemCount()
        {
            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd("1");
                }
            }

            var array = new string[bufferSize * stripeCount];
            buffer.DrainTo(array).ShouldBe(stripeCount * bufferSize);
        }

        [Fact]
        public void WhenDrainBufferIsSmallerThanStripedBufferDrainReturnsBufferItemCount()
        {
            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd("1");
                }
            }

            var array = new string[bufferSize];
            buffer.DrainTo(array).ShouldBe(bufferSize);
        }

        [Fact]
        public void WhenDrainBufferIsSmallerThanStripedBufferDrainReturnsBufferItemCount2()
        {
            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd("1");
                }
            }

            var array = new string[bufferSize+4];
            buffer.DrainTo(array).ShouldBe(bufferSize+4);
        }

        [Fact]
        public void WhenBufferIsPartFullDrainReturnsItems()
        {
            for (var j = 0; j < bufferSize; j++)
            {
                buffer.TryAdd("1");
            }

            var array = new string[bufferSize * stripeCount];
            buffer.DrainTo(array).ShouldBe(bufferSize);
        }

        [Fact]
        public void WhenBufferIsClearedDrainReturns0()
        {
            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd("1");
                }
            }

            buffer.Clear();

            var array = new string[bufferSize * stripeCount];
            buffer.DrainTo(array).ShouldBe(0);
        }
    }
}
