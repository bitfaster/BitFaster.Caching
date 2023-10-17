using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests
{
    public class ConcurrentDictionarySizeTests
    {
        private readonly ITestOutputHelper testOutputHelper;

        public ConcurrentDictionarySizeTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData(3, 7)]
        [InlineData(8, 11)]
        [InlineData(12, 17)]
        [InlineData(196, 197)]
        [InlineData(7199369, 7199369)]
        public void NextPrimeGreaterThan(int input, int nextPrime)
        {
            ConcurrentDictionarySize.NextPrimeGreaterThan(input).Should().Be(nextPrime);
        }

        [Theory]
        [InlineData(3, 7)]
        [InlineData(8, 11)]
        [InlineData(12, 17)]
        [InlineData(196, 137)]
        [InlineData(276, 179)]
        [InlineData(330, 221)]
        [InlineData(1553355606, 250478587)] // test larger than last SizeMap entry
        [InlineData(2003828731, 250478587)] // test overflow
        public void Estimate(int input, int optimal)
        {
            ConcurrentDictionarySize.Estimate(input).Should().Be(optimal);
        }
    }
}
