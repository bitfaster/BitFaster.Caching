using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class BufferConfigurationTests
    {
        [Theory]
        [InlineData(1, 3, 1, 32, 1, 16)]
        [InlineData(1, 14, 1, 128, 1, 16)]
        [InlineData(1, 50, 1, 128, 1, 64)]
        [InlineData(1, 100, 1, 128, 1, 128)]
        [InlineData(4, 100, 4, 128, 4, 32)]
        [InlineData(16, 100, 8, 128, 8, 16)]
        [InlineData(64, 100, 8, 128, 8, 16)]
        [InlineData(1, 1000, 1, 128, 1, 128)]
        [InlineData(4, 1000, 4, 128, 4, 128)]
        [InlineData(32, 1000, 32, 128, 32, 32)]
        [InlineData(256, 100000, 32, 128, 32, 32)]
        public void CalculateDefaultBufferSize(int concurrencyLevel, int capacity, int expectedReadStripes, int expectedReadBuffer, int expecteWriteStripes, int expecteWriteBuffer)
        {
            var bufferSize = LfuBufferSize.Default(concurrencyLevel, capacity);

            bufferSize.Read.StripeCount.Should().Be(expectedReadStripes);
            bufferSize.Read.BufferSize.Should().Be(expectedReadBuffer);
            bufferSize.Write.StripeCount.Should().Be(expecteWriteStripes);
            bufferSize.Write.BufferSize.Should().Be(expecteWriteBuffer);
        }
    }
}
