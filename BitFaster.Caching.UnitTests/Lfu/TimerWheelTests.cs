using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class TimerWheelTests
    {
        private readonly TimerWheel<int, IDisposable> timerWheel;
        private readonly LfuNodeList<int, IDisposable> lfuNodeList;
        private readonly ExpireAfterPolicy<int, IDisposable> policy;
        private ConcurrentLfuCore<int, IDisposable, TimeOrderNode<int, IDisposable>, ExpireAfterPolicy<int, IDisposable>> cache;

        public TimerWheelTests()
        {
            lfuNodeList = new();
            timerWheel = new();
            policy = new ExpireAfterPolicy<int, IDisposable>(new TestExpiryCalculator<int,IDisposable>());
            cache = new(
                Defaults.ConcurrencyLevel, 3, new ThreadPoolScheduler(), EqualityComparer<int>.Default, () => { }, policy);
        }

        [Theory]
        [MemberData(nameof(ClockData))]
        public void WhenAdvancedPastItemExpiryItemIsEvicted(long clock2)
        {
            timerWheel.time = clock2;

            var item = new DisposeTracker();
            timerWheel.Schedule(AddNode(1, item, new Duration(clock2 + TimerWheel<int, int>.Spans[0])));

            timerWheel.Advance(ref cache, new Duration(clock2 + 13 * TimerWheel<int, int>.Spans[0]));

            // this should be disposed
            item.Disposed.Should().BeTrue();
        }

        [Fact]
        public void WhenAdvanceOverflowsAndItemIsExpiredItemIsEvicted()
        {
            timerWheel.time = -(TimerWheel<int, int>.Spans[3] * 365) / 2;
            var item = new DisposeTracker();
            timerWheel.Schedule(AddNode(1, item, new Duration(timerWheel.time + TimerWheel<int, int>.Spans[0])));

            timerWheel.Advance(ref cache, new Duration(timerWheel.time + (TimerWheel<int, int>.Spans[3] * 365)));

            this.lfuNodeList.Count.Should().Be(0);
        }

#if NET6_0_OR_GREATER
        [Theory]
        [MemberData(nameof(ClockData))]
        public void WhenAdvanceBackwardsNothingIsEvicted(long clock)
        {
            var random = new Random();
            timerWheel.time = clock;

            long max = Duration.FromMinutes(60 * 24 * 10).raw;
            for (int i = 0; i < 1_000; i++)
            {
                long duration = random.NextInt64(max);
                timerWheel.Schedule(AddNode(i, new DisposeTracker(), new Duration(clock + duration)));
            }

            for (int i = 0; i < TimerWheel<int, IDisposable>.Buckets.Length; i++)
            {
                timerWheel.Advance(ref cache, new Duration(clock - 3 * TimerWheel<int, int>.Spans[i]));
            }

            this.lfuNodeList.Count.Should().Be(1_000);
        }
#endif

        [Fact]
        public void WhenAdvanceThrowsCurrentTimeIsNotAdvanced()
        {
            Duration clock = Duration.SinceEpoch();
            timerWheel.time = clock.raw;

            timerWheel.Schedule(AddNode(1, new DisposeThrows(), new Duration(clock.raw + TimerWheel<int, int>.Spans[1])));

            // This should expire the node, call evict, then throw via DisposeThrows.Dispose()
            Action advance = () => timerWheel.Advance(ref cache, new Duration(clock.raw + int.MaxValue));
            advance.Should().Throw<InvalidOperationException>();

            timerWheel.time.Should().Be(clock.raw);
        }

        [Theory]
        [MemberData(nameof(ClockData))]
        public void WhenEmptyGetExpirationDelayIsMax(long clock)
        {
            timerWheel.time = clock;
            timerWheel.GetExpirationDelay().raw.Should().Be(long.MaxValue);
        }

        [Theory]
        [MemberData(nameof(ClockData))]
        public void WhenScheduledInFirstWheelDelayIsUpdated(long clock)
        {
            timerWheel.time = clock;

            Duration delay = Duration.FromSeconds(1);

            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeTracker()) { TimeToExpire = new Duration(clock) + delay });

            timerWheel.GetExpirationDelay().raw.Should().BeLessThanOrEqualTo(TimerWheel<int, int>.Spans[0]);
        }

        [Theory]
        [MemberData(nameof(ClockData))]
        public void WhenScheduledInLastWheelDelayIsUpdated(long clock)
        {
            timerWheel.time = clock;

            Duration delay = Duration.FromMinutes(60 * 24 * 14);

            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeTracker()) { TimeToExpire = new Duration(clock) + delay });

            timerWheel.GetExpirationDelay().raw.Should().BeLessThanOrEqualTo(delay.raw);
        }

        [Theory]
        [MemberData(nameof(ClockData))]
        public void WhenScheduledInDifferentWheelsDelayIsCorrect(long clock)
        {
            var clockD = new Duration(clock);
            timerWheel.time = clock;

            Duration t15 = clockD + Duration.FromSeconds(15);
            Duration t80 = clockD + Duration.FromSeconds(80);

            timerWheel.Schedule(AddNode(1, new DisposeTracker(), t15 )); // wheel 0
            timerWheel.Schedule(AddNode(2, new DisposeTracker(), t80 )); // wheel 1

            Duration t45 = clockD + Duration.FromSeconds(45); // discard T15, T80 in wheel[1]
            timerWheel.Advance(ref cache, t45);

            lfuNodeList.Count.Should().Be(1); // verify discarded

            Duration t95 = clockD + Duration.FromSeconds(95);
            timerWheel.Schedule(AddNode(3, new DisposeTracker(), t95 )); // wheel 0

            Duration expectedDelay = (t80 - t45);
            var delay = timerWheel.GetExpirationDelay();
            delay.raw.Should().BeLessThan(expectedDelay.raw + TimerWheel<int, int>.Spans[0]);
        }

        private TimeOrderNode<int, IDisposable> AddNode(int key, IDisposable value, Duration timeToExpire)
        {
            var node = new TimeOrderNode<int, IDisposable>(key, value) { TimeToExpire = timeToExpire };
            this.lfuNodeList.AddLast(node);
            return node;
        }

        public static IEnumerable<object[]> ClockData =>
                new List<object[]>
                {
                    new object[] { long.MinValue },
                    new object[] { -TimerWheel<int, int>.Spans[1] + 1 },
                    new object[] { 0L },
                    new object[] { 0xfffffffc0000000L },
                    new object[] { long.MaxValue - TimerWheel<int, int>.Spans[1] + 1 },
                    new object[] { long.MaxValue },
                };
    }

    public class DisposeTracker : IDisposable
    {
        public bool Disposed { get; set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    public class DisposeThrows : IDisposable
    {
        public void Dispose()
        {
            throw new InvalidOperationException();
        }
    }
}
