using System;
using BitFaster.Caching.Buffers;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Buffers
{
    public class StripedBufferSizeTests
    {
        [Fact]
        public void WhenBufferSizeIsLessThan1CtorThrows()
        {
            Action constructor = () => { var x = new StripedBufferSize(-1, 1); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenStripeCountIsLessThan1CtorThrows()
        {
            Action constructor = () => { var x = new StripedBufferSize(1, -1); };

            constructor.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void SizeIsRoundedToNextPowerOf2()
        {
            var bs = new StripedBufferSize(6, 16);

            bs.BufferSize.Should().Be(8);
        }

        [Fact]
        public void StripeCountIsRoundedToNextPowerOf2()
        {
            var bs = new StripedBufferSize(16, 6);

            bs.StripeCount.Should().Be(8);
        }
    }
}
