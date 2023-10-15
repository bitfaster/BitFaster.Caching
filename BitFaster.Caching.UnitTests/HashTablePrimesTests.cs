using BitFaster.Caching;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class HashTablePrimesTests
    {
        [Theory]
        [InlineData(3, 7)]
        [InlineData(8, 11)]
        [InlineData(12, 17)]
        [InlineData(132, 131)]
        [InlineData(500, 131)]
        public void NextPrimeGreaterThan(int input, int nextPrime)
        {
            HashTablePrimes.NextPrimeGreaterThan(input).Should().Be(nextPrime);
        }
    }
}
