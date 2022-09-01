using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class BufferConfigurationTests
    {
        [Theory]
        [InlineData(1, 3, 1, 4, 1, 4)]
        [InlineData(1, 100, 1, 128, 1, 128)]
        [InlineData(4, 100, 4, 128, 4, 32)]
        [InlineData(8, 100, 8, 128, 8, 16)]
        [InlineData(12, 100, 16, 128, 16, 8)]
        [InlineData(16, 100, 16, 128, 16, 8)]
        [InlineData(24, 100, 32, 128, 32, 4)]
        [InlineData(64, 100, 64, 64, 64, 4)]
        [InlineData(96, 100, 128, 32, 128, 4)]
        [InlineData(1, 1000, 1, 128, 1, 128)]
        [InlineData(2, 1000, 2, 128, 2, 128)]
        [InlineData(4, 1000, 4, 128, 4, 128)]
        [InlineData(8, 1000, 8, 128, 8, 128)]
        [InlineData(16, 1000, 16, 128, 16, 64)]
        [InlineData(32, 1000, 32, 128, 32, 32)]
        [InlineData(64, 1000, 64, 64, 64, 16)]
        [InlineData(128, 1000, 128, 32, 128, 8)]
        [InlineData(256, 1000, 256, 16, 256, 4)]
        [InlineData(1, 10000, 1, 128, 1, 128)]
        [InlineData(4, 10000, 4, 128, 4, 128)]
        [InlineData(8, 10000, 8, 128, 8, 128)]
        [InlineData(16, 10000, 16, 128, 16, 128)]
        [InlineData(32, 10000, 32, 128, 32, 64)]
        [InlineData(64, 10000, 64, 64, 64, 32)]
        [InlineData(128, 10000, 128, 32, 128, 16)]
        [InlineData(256, 10000, 256, 16, 256, 8)]
        [InlineData(1, 100000, 1, 128, 1, 128)]
        [InlineData(32, 100000, 32, 128, 32, 64)]
        [InlineData(256, 100000, 256, 16, 256, 8)]
        public void CalculateDefaultBufferConfiguration(int concurrencyLevel, int capacity, int expectedReadStripes, int expectedReadBuffer, int expecteWriteStripes, int expecteWriteBuffer)
        {
            var bufferConfig = BufferConfiguration.CreateDefault(concurrencyLevel, capacity);

            bufferConfig.ReadBufferStripes.Should().Be(expectedReadStripes);
            bufferConfig.ReadBufferSize.Should().Be(expectedReadBuffer);
            bufferConfig.WriteBufferStripes.Should().Be(expecteWriteStripes);
            bufferConfig.WriteBufferSize.Should().Be(expecteWriteBuffer);
        }
    }
}
