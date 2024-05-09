using System;
using System.Diagnostics;
using BitFaster.Caching.Lfu;
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

        [Fact]
        public void OutputTimeParameters()
        {
            this.testOutputHelper.WriteLine($"Stopwatch.Frequency {Stopwatch.Frequency}");
            this.testOutputHelper.WriteLine($"TimeSpan.TicksPerSecond {TimeSpan.TicksPerSecond}");
            this.testOutputHelper.WriteLine($"stopwatchAdjustmentFactor {StopwatchTickConverter.stopwatchAdjustmentFactor}");

            // replicate WhenAdvanceThrowsCurrentTimeIsNotAdvanced
            Duration clock = Duration.SinceEpoch();

            this.testOutputHelper.WriteLine($"clock time {clock.raw}");
            this.testOutputHelper.WriteLine($"node time {new Duration(clock.raw + TimerWheel.Spans[1]).raw}");
            this.testOutputHelper.WriteLine($"adv time {new Duration(clock.raw + int.MaxValue).raw}");
        }
    }
}
