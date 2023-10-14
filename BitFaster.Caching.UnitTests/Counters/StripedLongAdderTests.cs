using BitFaster.Caching.Counters;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Counters
{
    public class StripedLongAdderTests
    {
        [Fact]
        public void InitialValueIsZero()
        {
            new Counter().Count().Should().Be(0);
        }

        [Fact]
        public void WhenIncrementedOneIsAdded()
        {
            var adder = new Counter();

            adder.Increment();

            adder.Count().Should().Be(1);
        }
    }
}
