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
        [InlineData(500, 197)]
        public void NextPrimeGreaterThan(int input, int nextPrime)
        {
            ConcurrentDictionarySize.NextPrimeGreaterThan(input).Should().Be(nextPrime);
        }

        [Theory]
        [InlineData(3, 7)]
        [InlineData(8, 11)]
        [InlineData(12, 17)]
        [InlineData(196, 197)]
        [InlineData(276, 137)]
        [InlineData(330, 163)]
        [InlineData(2003828731, 250478587)]
        public void Estimate(int input, int nextPrime)
        {
            ConcurrentDictionarySize.Estimate(input).Should().Be(nextPrime);
        }
    }
}
