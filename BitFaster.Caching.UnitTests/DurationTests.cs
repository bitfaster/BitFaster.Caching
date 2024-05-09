using System;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class DurationTests
    {
        [Fact]
        public void RoundTripHours()
        {
            var d = Duration.FromHours(2);
            d.ToTimeSpan().Should().BeCloseTo(TimeSpan.FromHours(2), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void RoundTripDays()
        {
            var d = Duration.FromDays(2);
            d.ToTimeSpan().Should().BeCloseTo(TimeSpan.FromDays(2), TimeSpan.FromMilliseconds(100));
        }
    }
}
