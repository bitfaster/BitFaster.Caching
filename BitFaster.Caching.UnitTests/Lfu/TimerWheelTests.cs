using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using FluentAssertions.Common;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class TimerWheelTests
    {
        private readonly ITestOutputHelper output;

        private readonly TimerWheel<int, IDisposable> timerWheel;
        private readonly WheelEnumerator<int, IDisposable> wheelEnumerator;
        private readonly LfuNodeList<int, IDisposable> lfuNodeList;
        private readonly ExpireAfterPolicy<int, IDisposable> policy;
        private ConcurrentLfuCore<int, IDisposable, TimeOrderNode<int, IDisposable>, ExpireAfterPolicy<int, IDisposable>> cache;

        public TimerWheelTests(ITestOutputHelper testOutputHelper)
        {
            output = testOutputHelper;
            lfuNodeList = new();
            timerWheel = new();
            wheelEnumerator = new(timerWheel, testOutputHelper);
            policy = new ExpireAfterPolicy<int, IDisposable>(new TestExpiryCalculator<int, IDisposable>());
            cache = new(
                Defaults.ConcurrencyLevel, 3, new ThreadPoolScheduler(), EqualityComparer<int>.Default, () => { }, policy);
        }

        [Theory]
        [MemberData(nameof(ScheduleData))]
        public void WhenAdvanceExpiredNodesExpire(long clock, Duration duration, int expiredCount)
        {
            var items = new List<TimeOrderNode<int, IDisposable>>();
            timerWheel.time = clock;

            foreach (int timeout in new int[] { 25, 90, 240 })
            {
                var node = AddNode(1, new DisposeTracker(), new Duration(clock) + Duration.FromSeconds(timeout));
                items.Add(node);
                timerWheel.Schedule(node);
            }

            timerWheel.Advance(ref cache, new Duration(clock) + duration);

            var expired = items.Where(n => ((DisposeTracker)n.Value).Expired);
            expired.Count().Should().Be(expiredCount);

            foreach (var node in expired)
            {
                node.GetTimestamp().Should().BeLessThanOrEqualTo(clock + duration.raw);
            }
        }

        [Theory]
        [MemberData(nameof(ClockData))]
        public void WhenAdvancedPastItemExpiryItemIsEvicted(long clock2)
        {
            timerWheel.time = clock2;

            var item = new DisposeTracker();
            timerWheel.Schedule(AddNode(1, item, new Duration(clock2 + TimerWheel.Spans[0])));

            timerWheel.Advance(ref cache, new Duration(clock2 + 13 * TimerWheel.Spans[0]));

            item.Expired.Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ClockData))]
        public void WhenAdvanceDifferentWheelsNodeIsRescheduled(long clock)
        {
            var clockD = new Duration(clock);
            timerWheel.time = clock;

            Duration t15 = clockD + Duration.FromSeconds(15);
            Duration t120 = clockD + Duration.FromSeconds(120);

            timerWheel.Schedule(AddNode(15, new DisposeTracker(), t15)); // wheel 0
            timerWheel.Schedule(AddNode(120, new DisposeTracker(), t120)); // wheel 1

            wheelEnumerator.Count().Should().Be(2);
            var initialPosition = wheelEnumerator.PositionOf(120);

            Duration t45 = clockD + Duration.FromSeconds(45); // discard T15, T120 in wheel[1]
            timerWheel.Advance(ref cache, t45);

            lfuNodeList.Count.Should().Be(1); // verify discarded T15
            wheelEnumerator.PositionOf(15).Should().Be(WheelPosition.None);

            Duration t110 = clockD + Duration.FromSeconds(110);
            timerWheel.Advance(ref cache, t110);

            lfuNodeList.Count.Should().Be(1); // verify not discarded, T120 in wheel[0]
            var rescheduledPosition = wheelEnumerator.PositionOf(120);

            rescheduledPosition.Should().BeLessThan(initialPosition);

            Duration t130 = clockD + Duration.FromSeconds(130);
            timerWheel.Advance(ref cache, t130);

            lfuNodeList.Count.Should().Be(0); // verify discarded T120
            wheelEnumerator.PositionOf(120).Should().Be(WheelPosition.None);
        }

        [Fact]
        public void WhenAdvanceOverflowsAndItemIsExpiredItemIsEvicted()
        {
            timerWheel.time = -(TimerWheel.Spans[3] * 365) / 2;
            var item = new DisposeTracker();
            timerWheel.Schedule(AddNode(1, item, new Duration(timerWheel.time + TimerWheel.Spans[0])));

            timerWheel.Advance(ref cache, new Duration(timerWheel.time + (TimerWheel.Spans[3] * 365)));

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

            for (int i = 0; i < TimerWheel.Buckets.Length; i++)
            {
                timerWheel.Advance(ref cache, new Duration(clock - 3 * TimerWheel.Spans[i]));
            }

            this.lfuNodeList.Count.Should().Be(1_000);
        }
#endif

        [Fact]
        public void WhenAdvanceThrowsCurrentTimeIsNotAdvanced()
        {
            Duration clock = Duration.SinceEpoch();
            timerWheel.time = clock.raw;

            timerWheel.Schedule(AddNode(1, new DisposeThrows(), new Duration(clock.raw + TimerWheel.Spans[1])));

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
        public void WhenScheduledMaxNodeIsInOuterWheel(long clock)
        {
            var clockD = new Duration(clock);
            timerWheel.time = clock;

            Duration tMax = clockD + new Duration(long.MaxValue);

            timerWheel.Schedule(AddNode(1, new DisposeTracker(), tMax));

            var initialPosition = wheelEnumerator.PositionOf(1);
            initialPosition.wheel.Should().Be(4);
        }

        [Theory]
        [MemberData(nameof(ClockData))]
        public void WhenScheduledInFirstWheelDelayIsUpdated(long clock)
        {
            timerWheel.time = clock;

            Duration delay = Duration.FromSeconds(1);

            timerWheel.Schedule(new TimeOrderNode<int, IDisposable>(1, new DisposeTracker()) { TimeToExpire = new Duration(clock) + delay });

            timerWheel.GetExpirationDelay().raw.Should().BeLessThanOrEqualTo(TimerWheel.Spans[0]);
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

            timerWheel.Schedule(AddNode(1, new DisposeTracker(), t15)); // wheel 0
            timerWheel.Schedule(AddNode(2, new DisposeTracker(), t80)); // wheel 1

            Duration t45 = clockD + Duration.FromSeconds(45); // discard T15, T80 in wheel[1]
            timerWheel.Advance(ref cache, t45);

            lfuNodeList.Count.Should().Be(1); // verify discarded

            Duration t95 = clockD + Duration.FromSeconds(95);
            timerWheel.Schedule(AddNode(3, new DisposeTracker(), t95)); // wheel 0

            Duration expectedDelay = (t80 - t45);
            var delay = timerWheel.GetExpirationDelay();
            delay.raw.Should().BeLessThan(expectedDelay.raw + TimerWheel.Spans[0]);
        }

        [Fact]
        public void WhenScheduledThenDescheduledNodeIsRemoved()
        {
            var node = AddNode(1, new DisposeTracker(), Duration.SinceEpoch());

            timerWheel.Schedule(node);
            wheelEnumerator.PositionOf(1).Should().NotBe(WheelPosition.None);

            TimerWheel<int, IDisposable>.Deschedule(node);
            wheelEnumerator.PositionOf(1).Should().Be(WheelPosition.None);
            node.GetNextInTimeOrder().Should().BeNull();
            node.GetPreviousInTimeOrder().Should().BeNull();
        }

        [Fact]
        public void WhenRescheduledLaterNodeIsMoved()
        {
            var time = Duration.SinceEpoch();
            var node = AddNode(1, new DisposeTracker(), time);

            timerWheel.Schedule(node);
            var initial = wheelEnumerator.PositionOf(1);

            node.TimeToExpire = time + Duration.FromMinutes(60 * 24);
            timerWheel.Reschedule(node);
            wheelEnumerator.PositionOf(1).Should().BeGreaterThan(initial);
        }

        [Fact]
        public void WhenDetachedRescheduleIsNoOp()
        {
            var time = Duration.SinceEpoch();
            var node = AddNode(1, new DisposeTracker(), time);

            timerWheel.Reschedule(node);
            wheelEnumerator.PositionOf(1).Should().Be(WheelPosition.None);
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
                    new object[] { -TimerWheel.Spans[0] + 1 },
                    new object[] { 0L },
                    new object[] { 0xfffffffc0000000L },
                    new object[] { long.MaxValue - TimerWheel.Spans[0] + 1 },
                    new object[] { long.MaxValue },
                };

        public static IEnumerable<object[]> ScheduleData = CreateSchedule();

        private static IEnumerable<object[]> CreateSchedule()
        {
            var schedule = new List<object[]>();

            foreach (var clock in ClockData)
            {
                schedule.Add(new object[] { clock.First(), Duration.FromSeconds(10), 0 });
                schedule.Add(new object[] { clock.First(), Duration.FromMinutes(3), 2 });
                schedule.Add(new object[] { clock.First(), Duration.FromMinutes(10), 3 });
            }

            return schedule;
        }
    }
    
    public class DisposeTracker : IDisposable
    {
        public bool Expired { get; set; }

        public void Dispose()
        {
            Expired = true;
        }
    }

    public class DisposeThrows : IDisposable
    {
        public void Dispose()
        {
            throw new InvalidOperationException();
        }
    }

    internal class WheelEnumerator<K, V> : IEnumerable<KeyValuePair<WheelPosition, TimeOrderNode<K, V>>>
        where K : notnull
    {
        private readonly TimerWheel<K, V> timerWheel;
        private readonly ITestOutputHelper testOutputHelper;

        public WheelEnumerator(TimerWheel<K, V> timerWheel, ITestOutputHelper testOutputHelper)
        {
            this.timerWheel = timerWheel;
            this.testOutputHelper = testOutputHelper;
        }

        public void Dump(string tag = null)
        {
            this.testOutputHelper.WriteLine(tag);
            int count = 0;

            foreach (KeyValuePair<WheelPosition, TimeOrderNode<K, V>> kvp in this)
            {
                this.testOutputHelper.WriteLine($"[{kvp.Key.wheel},{kvp.Key.bucket}] {kvp.Value.Key}");
                count++;
            }

            if (count == 0)
            {
                this.testOutputHelper.WriteLine("empty");
            }
        }

        public WheelPosition PositionOf(K key)
        {
            var v = this.Where(kvp => EqualityComparer<K>.Default.Equals(kvp.Value.Key, key));

            if (v.Any())
            {
                return v.First().Key;
            }

            return WheelPosition.None;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((WheelEnumerator<K, V>)this).GetEnumerator();
        }

        public IEnumerator<KeyValuePair<WheelPosition, TimeOrderNode<K, V>>> GetEnumerator()
        {
            for (int w = 0; w < timerWheel.wheels.Length; w++)
            {
                var wheel = timerWheel.wheels[w];

                for (int b = 0; b < wheel.Length; b++)
                {
                    var sentinel = wheel[b];
                    var node = sentinel.GetNextInTimeOrder();

                    while (node != sentinel)
                    {
                        yield return new KeyValuePair<WheelPosition, TimeOrderNode<K, V>>(new WheelPosition(w, b), node);
                        node = node.GetNextInTimeOrder();
                    }
                }
            }
        }
    }

    internal struct WheelPosition : IComparable<WheelPosition>
    {
        public readonly int wheel;
        public readonly int bucket;

        public static readonly WheelPosition None = new(-1, -1);

        public WheelPosition(int wheel, int bucket)
        {
            this.wheel = wheel;
            this.bucket = bucket;
        }

        public static bool operator >(WheelPosition a, WheelPosition b) => a.wheel > b.wheel | (a.wheel == b.wheel && a.bucket > b.bucket);
        public static bool operator <(WheelPosition a, WheelPosition b) => a.wheel < b.wheel | (a.wheel == b.wheel && a.bucket < b.bucket);

        public int CompareTo(WheelPosition that)
        {
            if (this.wheel == that.wheel)
            {
                return this.bucket.CompareTo(that.bucket);
            }

            return this.wheel.CompareTo(that.wheel);
        }
    }
}
