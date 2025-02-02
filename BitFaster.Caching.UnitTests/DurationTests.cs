using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BitFaster.Caching.Lru;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests
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
#if NETCOREAPP3_0_OR_GREATER
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Duration.SinceEpoch().raw.ShouldBe(Stopwatch.GetTimestamp());
            }
            else
            {
                Duration.SinceEpoch().raw.ShouldBe(Environment.TickCount64);
            }
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Duration.SinceEpoch().raw.ShouldBe(Duration.GetTickCount64());
            }
            else
            {
                // eps is 1/200 of a second
                ulong eps = (ulong)(Stopwatch.Frequency / 200);
                Duration.SinceEpoch().raw.ShouldBe(Stopwatch.GetTimestamp());
            }
#endif
        }

        [Fact]
        public void ToTimeSpan()
        {
#if NETCOREAPP3_0_OR_GREATER
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                new Duration(100).ToTimeSpan().ShouldBe(new TimeSpan(100), TimeSpan.FromMilliseconds(50));
            }
            else
            {
                new Duration(1000).ToTimeSpan().ShouldBe(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(10));
            }
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                new Duration(1000).ToTimeSpan().ShouldBe(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(10));
            }
            else
            {
                // for Stopwatch.GetTimestamp() this is number of ticks
                new Duration(1 * Stopwatch.Frequency).ToTimeSpan().ShouldBe(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(10));
            }
#endif    
        }

        [Fact]
        public void FromTimeSpan()
        {
#if NETCOREAPP3_0_OR_GREATER
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Duration.FromTimeSpan(TimeSpan.FromSeconds(1)).raw
                    .ShouldBe(Stopwatch.Frequency);
            }
            else
            {
                Duration.FromTimeSpan(TimeSpan.FromSeconds(1)).raw
                    .ShouldBe((long)TimeSpan.FromSeconds(1).TotalMilliseconds);
            }
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Duration.FromTimeSpan(TimeSpan.FromSeconds(1)).raw
                    .ShouldBe((long)TimeSpan.FromSeconds(1).TotalMilliseconds);
            }
            else
            {
                Duration.FromTimeSpan(TimeSpan.FromSeconds(1)).raw
                .ShouldBe(Stopwatch.Frequency);
            }
#endif    
        }

        [Fact]
        public void RoundTripMilliseconds()
        {
            Duration.FromMilliseconds(2000)
                .ToTimeSpan()
                .ShouldBe(TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public void RoundTripSeconds()
        {
            Duration.FromSeconds(2)
                .ToTimeSpan()
                .ShouldBe(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public void RoundTripMinutes()
        {
            Duration.FromMinutes(2)
                .ToTimeSpan()
                .ShouldBe(TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void RoundTripHours()
        {
            Duration.FromHours(2)
                .ToTimeSpan()
                .ShouldBe(TimeSpan.FromHours(2), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void RoundTripDays()
        {
            Duration.FromDays(2)
                .ToTimeSpan()
                .ShouldBe(TimeSpan.FromDays(2), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void OperatorPlus()
        {
            (Duration.FromDays(2) + Duration.FromDays(2))
                .ToTimeSpan()
                .ShouldBe(TimeSpan.FromDays(4), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void OperatorMinus()
        {
            (Duration.FromDays(4) - Duration.FromDays(2))
                .ToTimeSpan()
                .ShouldBe(TimeSpan.FromDays(2), TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void OperatorGreater()
        {
            (Duration.FromDays(4) > Duration.FromDays(2))
                .ShouldBeTrue();
        }

        [Fact]
        public void OperatorLess()
        {
            (Duration.FromDays(4) < Duration.FromDays(2))
                .ShouldBeFalse();
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
