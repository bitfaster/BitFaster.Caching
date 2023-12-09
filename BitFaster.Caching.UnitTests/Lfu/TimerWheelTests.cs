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
            policy = new ExpireAfterPolicy<int, IDisposable>(timerWheel);
            cache = new(
                Defaults.ConcurrencyLevel, 3, new ThreadPoolScheduler(), EqualityComparer<int>.Default, () => { }, policy);
        }

        [Theory]
        [InlineData(long.MinValue)]
        [InlineData(-65536 + 1)] // -SPANS[0] + 1
        [InlineData(0L)]
        [InlineData(0xfffffffc0000000L)]
        [InlineData(long.MaxValue - 65536 + 1)] // SPANS[0] + 1
        [InlineData(long.MaxValue)]
        public void WhenAdvancedPastItemExpiryItemIsEvicted(long clock2)
        {
            timerWheel.nanos = clock2;

            var item = new DisposeTracker();
            timerWheel.Schedule(AddNode(1, item, new Duration(clock2 + TimerWheel<int, int>.Spans[0])));

            timerWheel.Advance(ref cache, clock2 + 13 * TimerWheel<int, int>.Spans[0]);

            // this should be disposed
            item.Disposed.Should().BeTrue();
        }

        [Fact]
        public void WhenAdvanceOverflowsAndItemIsExpiredItemIsEvicted()
        {
            timerWheel.nanos = -(TimerWheel<int, int>.Spans[3] * 365) / 2;
            var item = new DisposeTracker();
            timerWheel.Schedule(AddNode(1, item, new Duration(timerWheel.nanos + TimerWheel<int, int>.Spans[0])));

            timerWheel.Advance(ref cache, timerWheel.nanos + (TimerWheel<int, int>.Spans[3] * 365));

            this.lfuNodeList.Count.Should().Be(0);
        }

#if NET6_0_OR_GREATER
        [Theory]
        [InlineData(long.MinValue)]
        [InlineData(-65536 + 1)] // -SPANS[0] + 1
        [InlineData(0L)]
        [InlineData(0xfffffffc0000000L)]
        [InlineData(long.MaxValue - 65536 + 1)] // SPANS[0] + 1
        [InlineData(long.MaxValue)]
        public void WhenAdvanceBackwardsNothingIsEvicted(long clock)
        {
            var random = new Random();
            timerWheel.nanos = clock;

            long max = Duration.FromMinutes(60 * 24 * 10).raw;
            for (int i = 0; i < 1_000; i++)
            {
                long duration = random.NextInt64(max);
                timerWheel.Schedule(AddNode(i, new DisposeTracker(), new Duration(clock + duration)));
            }

            for (int i = 0; i < TimerWheel<int, IDisposable>.Buckets.Length; i++)
            {
                timerWheel.Advance(ref cache, clock - 3 * TimerWheel<int, int>.Spans[i]);
            }

            this.lfuNodeList.Count.Should().Be(1_000);
        }
#endif

        [Fact]
        public void WhenAdvanceThrowsCurrentTimeIsNotAdvanced()
        {
            Duration clock = Duration.SinceEpoch();
            timerWheel.nanos = clock.raw;

            timerWheel.Schedule(AddNode(1, new DisposeThrows(), new Duration(clock.raw + TimerWheel<int, int>.Spans[1])));

            // This should expire the node, call evict, then throw via DisposeThrows.Dispose()
            Action advance = () => timerWheel.Advance(ref cache, clock.raw + int.MaxValue);
            advance.Should().Throw<InvalidOperationException>();

            timerWheel.nanos.Should().Be(clock.raw);
        }

        [Theory]
        [InlineData(long.MinValue)]
        [InlineData(-65536 + 1)] // -SPANS[0] + 1
        [InlineData(0L)]
        [InlineData(0xfffffffc0000000L)]
        [InlineData(long.MaxValue - 65536 + 1)] // SPANS[0] + 1
        [InlineData(long.MaxValue)]
        public void WhenEmptyGetExpirationDelayIsMax(long clock)
        {
            timerWheel.nanos = clock;
            timerWheel.GetExpirationDelay().raw.Should().Be(long.MaxValue);
        }

        [Theory]
        [InlineData(long.MinValue)]
        [InlineData(-65536 + 1)] // -SPANS[0] + 1
        [InlineData(0L)]
        [InlineData(0xfffffffc0000000L)]
        [InlineData(long.MaxValue - 65536 + 1)] // SPANS[0] + 1
        [InlineData(long.MaxValue)]
        public void WhenScheduledInFirstWheelDelayIsUpdated(long clock)
        {
            timerWheel.nanos = clock;

            Duration delay = Duration.FromSeconds(1);

            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeTracker()) { TimeToExpire = new Duration(clock) + delay });

            timerWheel.GetExpirationDelay().raw.Should().BeLessThanOrEqualTo(TimerWheel<int, int>.Spans[0]);
        }

        [Theory]
        [InlineData(long.MinValue)]
        [InlineData(-65536 + 1)] // -SPANS[0] + 1
        [InlineData(0L)]
        [InlineData(0xfffffffc0000000L)]
        [InlineData(long.MaxValue - 65536 + 1)] // SPANS[0] + 1
        [InlineData(long.MaxValue)]
        public void WhenScheduledInLastWheelDelayIsUpdated(long clock)
        {
            timerWheel.nanos = clock;

            Duration delay = Duration.FromMinutes(60 * 24 * 14);

            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeTracker()) { TimeToExpire = new Duration(clock) + delay });

            timerWheel.GetExpirationDelay().raw.Should().BeLessThanOrEqualTo(delay.raw);
        }

        [Theory]
        [InlineData(long.MinValue)]
        [InlineData(-65536 + 1)] // -SPANS[0] + 1
        [InlineData(0L)]
        [InlineData(0xfffffffc0000000L)]
        [InlineData(long.MaxValue - 65536 + 1)] // SPANS[0] + 1
        [InlineData(long.MaxValue)]
        public void WhenScheduledInDifferentWheelsDelayIsCorrect(long clock)
        {
            var clockD = new Duration(clock);
            timerWheel.nanos = clock;

            Duration t15 = clockD + Duration.FromSeconds(15);
            Duration t80 = clockD + Duration.FromSeconds(80);

            timerWheel.Schedule(AddNode(1, new DisposeTracker(), t15 )); // wheel 0
            timerWheel.Schedule(AddNode(2, new DisposeTracker(), t80 )); // wheel 1

            Duration t45 = clockD + Duration.FromSeconds(45); // discard T15, T80 in wheel[1]
            timerWheel.Advance(ref cache, t45.raw);

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
