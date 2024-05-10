using System;
using System.Diagnostics;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests
{
    public class DurationTests
    {
        private readonly ITestOutputHelper testOutputHelper;

        public DurationTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void RoundTripMilliseconds()
        {
            Duration.FromMilliseconds(2000)
                .ToTimeSpan()
                .Should().BeCloseTo(TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public void RoundTripSeconds()
        {
            Duration.FromSeconds(2)
                .ToTimeSpan()
                .Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public void RoundTripMinutes()
        {
            Duration.FromMinutes(2)
                .ToTimeSpan()
                .Should().BeCloseTo(TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void RoundTripHours()
        {
            Duration.FromHours(2)
                .ToTimeSpan()
                .Should().BeCloseTo(TimeSpan.FromHours(2), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void RoundTripDays()
        {
            Duration.FromDays(2)
                .ToTimeSpan()
                .Should().BeCloseTo(TimeSpan.FromDays(2), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void OperatorPlus()
        {
            (Duration.FromDays(2) + Duration.FromDays(2))
                .ToTimeSpan()
                .Should().BeCloseTo(TimeSpan.FromDays(4), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void OperatorMinus()
        {
            (Duration.FromDays(4) - Duration.FromDays(2))
                .ToTimeSpan()
                .Should().BeCloseTo(TimeSpan.FromDays(2), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void OperatorGreater()
        {
            (Duration.FromDays(4) > Duration.FromDays(2))
                .Should().BeTrue();
        }

        [Fact]
        public void OperatorLess()
        {
            (Duration.FromDays(4) < Duration.FromDays(2))
                .Should().BeFalse();
        }

        // This is for diagnostic purposes when tests run on different operating systems.
        [Fact]
        public void OutputTimeParameters()
        {
            this.testOutputHelper.WriteLine($"Stopwatch.Frequency {Stopwatch.Frequency}");
            this.testOutputHelper.WriteLine($"TimeSpan.TicksPerSecond {TimeSpan.TicksPerSecond}");
            this.testOutputHelper.WriteLine($"stopwatchAdjustmentFactor {StopwatchTickConverter.stopwatchAdjustmentFactor}");
            this.testOutputHelper.WriteLine($"Duration.SinceEpoch {Duration.SinceEpoch()}");
        }
    }
}
