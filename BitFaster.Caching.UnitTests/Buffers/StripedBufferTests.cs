using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Buffers
{
    public class StripedBufferTests
    {
        const int bufferSize = 16;
        const int stripeCount = 2;
        private readonly StripedBuffer<int> buffer = new StripedBuffer<int>(stripeCount, bufferSize);

        [Fact]
        public void WhenBufferIsFullTryAddReturnsFull()
        {
            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd(1).Should().Be(BufferStatus.Success);
                }
            }

            buffer.TryAdd(1).Should().Be(BufferStatus.Full);
        }

        [Fact]
        public void WhenBufferIsEmptyDrainReturnsZero()
        {
            var array = new int[bufferSize];
            buffer.DrainTo(array).Should().Be(0);
        }

        [Fact]
        public void WhenBufferIsFullDrainReturnsItemCount()
        {
            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd(1);
                }
            }

            var array = new int[bufferSize * stripeCount];
            buffer.DrainTo(array).Should().Be(stripeCount * bufferSize);
        }

        [Fact]
        public void WhenDrainBufferIsSmallerThanStripedBufferDrainReturnsBufferItemCount()
        {
            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd(1);
                }
            }

            var array = new int[bufferSize];
            buffer.DrainTo(array).Should().Be(bufferSize);
        }

        [Fact]
        public void WhenBufferIsPartFullDrainReturnsItems()
        {
            for (var j = 0; j < bufferSize; j++)
            {
                buffer.TryAdd(1);
            }

            var array = new int[bufferSize * stripeCount];
            buffer.DrainTo(array).Should().Be(bufferSize);
        }

        [Fact]
        public void WhenBufferIsClearedDrainReturns0()
        {
            for (var i = 0; i < stripeCount; i++)
            {
                for (var j = 0; j < bufferSize; j++)
                {
                    buffer.TryAdd(1);
                }
            }

            buffer.Clear();

            var array = new int[bufferSize * stripeCount];
            buffer.DrainTo(array).Should().Be(0);
        }
    }
}
