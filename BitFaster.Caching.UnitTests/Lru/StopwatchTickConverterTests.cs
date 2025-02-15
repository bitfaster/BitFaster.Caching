using System;
using BitFaster.Caching.Lru;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class StopwatchTickConverterTests
    {
        [Fact]
        public void WhenConvertingToTicksIsReversable()
        { 
            var timespan = TimeSpan.FromSeconds(1);

            StopwatchTickConverter.FromTicks(StopwatchTickConverter.ToTicks(timespan)).ShouldBe(timespan, TimeSpan.FromMilliseconds(20));
        }
    }
}
