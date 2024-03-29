﻿using System.Threading.Tasks;
using BitFaster.Caching.Counters;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Counters
{
    [Collection("Soak")]
    public class CounterSoakTests
    {
        [Fact]
        public async Task WhenAddingConcurrentlySumIsCorrect()
        {
            var adder = new Counter();

            await Threaded.Run(4, () =>
            {
                for (int i = 0; i < 100_000; i++)
                {
                    adder.Increment();
                }
            });

            adder.Count().Should().Be(400_000);
        }
    }
}
