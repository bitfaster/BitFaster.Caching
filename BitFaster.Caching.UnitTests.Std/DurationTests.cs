using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Std
{
    public class DurationTests
    {
        public static readonly ulong epsilon = (ulong)Duration.FromMilliseconds(20).raw;

        private readonly ITestOutputHelper testOutputHelper;

        public DurationTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void SinceEpoch()
        {
            // On .NET Standard, only windows uses TickCount64
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Duration.SinceEpoch().raw.Should().BeCloseTo(Environment.TickCount64, 15);
            }
            else
            {
                // eps is 1/200 of a second
                ulong eps = (ulong)(Stopwatch.Frequency / 200);
                Duration.SinceEpoch().raw.Should().BeCloseTo(Stopwatch.GetTimestamp(), eps);
            }
        }

        [Fact]
        public void ToTimeSpan()
        {
            // On .NET Standard, only windows uses TickCount64
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                new Duration(1000).ToTimeSpan().Should().BeCloseTo(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(10));
            }
            else
            {
                // for Stopwatch.GetTimestamp() this is number of ticks
                new Duration(1 * Stopwatch.Frequency).ToTimeSpan().Should().BeCloseTo(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(10));
            }  
        }

        [Fact]
        public void FromTimeSpan()
        {
            // On .NET Standard, only windows uses TickCount64
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Duration.FromTimeSpan(TimeSpan.FromSeconds(1)).raw
                    .Should().Be((long)TimeSpan.FromSeconds(1).TotalMilliseconds);
            }
            else
            {
                Duration.FromTimeSpan(TimeSpan.FromSeconds(1)).raw
                .Should().Be(Stopwatch.Frequency);
            } 
        }

        // This is for diagnostic purposes when tests run on different operating systems.
        [Fact]
        public void OutputTimeParameters()
        {
            this.testOutputHelper.WriteLine($"Stopwatch.Frequency {Stopwatch.Frequency}");
            this.testOutputHelper.WriteLine($"TimeSpan.TicksPerSecond {TimeSpan.TicksPerSecond}");
            this.testOutputHelper.WriteLine($"stopwatchAdjustmentFactor {StopwatchTickConverter.stopwatchAdjustmentFactor}");
            var d = Duration.SinceEpoch();
            this.testOutputHelper.WriteLine($"Duration.SinceEpoch {d.raw} ({d.ToTimeSpan()})");
        }
    }
}
