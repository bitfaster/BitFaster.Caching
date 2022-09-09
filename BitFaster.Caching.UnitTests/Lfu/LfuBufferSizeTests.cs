﻿using System;
using BitFaster.Caching.Buffers;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class LfuBufferSizeTests
    {
        [Fact]
        public void WhenReadBufferIsNullThrows()
        {
            Action constructor = () => { var x = new LfuBufferSize(null); };

            constructor.Should().Throw<ArgumentNullException>();
        }

        [SkippableTheory]
        [InlineData(1, 3, 1, 32)]
        [InlineData(1, 14, 1, 128)]
        [InlineData(1, 50, 1, 128)]
        [InlineData(1, 100, 1, 128)]
        [InlineData(4, 100, 4, 128)]
        [InlineData(16, 100, 8, 128)] // fails win
        [InlineData(64, 100, 8, 128)] // fails win
        [InlineData(1, 1000, 1, 128)]
        [InlineData(4, 1000, 4, 128)]
        [InlineData(32, 1000, 32, 128)] // fails win + fails mac
        [InlineData(256, 100000, 32, 128)] // fails win + fails mac
        public void CalculateDefaultBufferSize(int concurrencyLevel, int capacity, int expectedReadStripes, int expectedReadBuffer)
        {
            // Some of these tests depend on the CPU Core count - skip if run on a different config machine.
            bool notExpectedCpuCount = Environment.ProcessorCount != 12;
            bool concurrencyLevelThresholdExceeded = BitOps.CeilingPowerOfTwo(concurrencyLevel) > BitOps.CeilingPowerOfTwo(Environment.ProcessorCount * 2);

            Skip.If(concurrencyLevelThresholdExceeded && notExpectedCpuCount, "Test outcome depends on machine CPU count");

            var bufferSize = LfuBufferSize.Default(concurrencyLevel, capacity);

            bufferSize.Read.StripeCount.Should().Be(expectedReadStripes);
            bufferSize.Read.BufferSize.Should().Be(expectedReadBuffer);
        }
    }
}
