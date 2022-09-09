using System.Threading.Tasks;
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

        [Fact]
        public async Task WhenAddingConcurrentlySumIsCorrect()
        {
            var adder = new Counter();

            await Threaded.Run(4, () => 
            {
                for (int i = 0; i < 100000; i++)
                {
                    adder.Increment();
                }
            });

            adder.Count().Should().Be(400000);
        }
    }
}
