using BitFaster.Caching.Counters;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Counters
{
    public class StripedLongAdderTests
    {
        [Fact]
        public void InitialValueIsZero()
        {
            new Counter().Count().ShouldBe(0);
        }

        [Fact]
        public void WhenIncrementedOneIsAdded()
        {
            var adder = new Counter();

            adder.Increment();

            adder.Count().ShouldBe(1);
        }
    }
}
