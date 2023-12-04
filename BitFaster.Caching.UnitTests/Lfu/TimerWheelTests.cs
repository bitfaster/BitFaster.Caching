using System;
using System.Collections.Generic;
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
        private readonly ExpireAfterPolicy<int, IDisposable> policy;
        private ConcurrentLfuCore<int, IDisposable, TimeOrderNode<int, IDisposable>, ExpireAfterPolicy<int, IDisposable>> cache;

        public TimerWheelTests()
        {
            timerWheel = new();
            policy = new ExpireAfterPolicy<int, IDisposable>(timerWheel);
            cache = new(
                Defaults.ConcurrencyLevel, 3, new ThreadPoolScheduler(), EqualityComparer<int>.Default, () => { }, policy);
        }

        [Fact]
        public void Ctor()
        {
            timerWheel.Should().NotBeNull();
        }

        [Fact]
        public void Advance()
        {
            Duration clock = Duration.SinceEpoch();
            timerWheel.nanos = clock.raw;

            var item = new DisposeTracker();
            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, item) { TimeToExpire = new Duration(clock.raw + TimerWheel<int, int>.SPANS[0]) } );

            timerWheel.Advance(ref cache, clock.raw + 13 * TimerWheel<int, int>.SPANS[0]);

            // this should be disposed
            // item.Disposed.Should().BeTrue();
        }

        [Fact]
        public void AdvanceThrow()
        {
            Duration clock = Duration.SinceEpoch();
            timerWheel.nanos = clock.raw;

            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeThrows()) { TimeToExpire = new Duration(clock.raw + TimerWheel<int, int>.SPANS[1]) });

            // This should expire the node, call evict, then throw via DisposeThrows.Dispose()
            timerWheel.Advance(ref cache, long.MaxValue);
        }

        [Fact]
        public void WhenEmptyGetExpirationDelayIsMax()
        {
            timerWheel.GetExpirationDelay().raw.Should().Be(long.MaxValue);
        }

        [Fact]
        public void WhenScheduledDelayIsUpdated()
        {
            Duration clock = Duration.SinceEpoch();
            timerWheel.nanos = clock.raw;

            Duration delay = clock + Duration.FromMinutes(60);

            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeTracker()) { TimeToExpire = delay });

            timerWheel.GetExpirationDelay().raw.Should().BeLessThanOrEqualTo(delay.raw);
        }

        [Fact]
        public void WhenScheduledInDifferentWheelsDelayIsCorrect()
        {
            Duration clock = Duration.SinceEpoch();
            timerWheel.nanos = clock.raw;

            Duration t15 = clock + Duration.FromSeconds(15);
            Duration t80 = clock + Duration.FromSeconds(80);

            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeTracker()) { TimeToExpire = t15 }); // wheel 0
            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeTracker()) { TimeToExpire = t80 }); // wheel 1

            Duration t45 = clock + Duration.FromSeconds(45); // discard T15, T80 in wheel[1]
            timerWheel.Advance(ref cache, t45.raw);

            Duration t95 = clock + Duration.FromSeconds(95);
            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeTracker()) { TimeToExpire = t95 }); // wheel 0

            Duration expectedDelay = (t80 - t45);
            var delay = timerWheel.GetExpirationDelay();
            delay.raw.Should().BeLessThan(expectedDelay.raw + TimerWheel<int, int>.SPANS[0]);
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
