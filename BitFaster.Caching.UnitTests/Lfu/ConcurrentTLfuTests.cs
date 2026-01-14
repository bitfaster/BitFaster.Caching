using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using BitFaster.Caching.UnitTests.Retry;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    // This could use foreground scheduler to make it more deterministic.
    public class ConcurrentTLfuTests
    {
        private readonly TimeSpan timeToLive = TimeSpan.FromMilliseconds(200);
        private readonly int capacity = 9;
        private ConcurrentTLfu<int, string> lfu;

        private Lru.ValueFactory valueFactory = new Lru.ValueFactory();

        // on MacOS time measurement seems to be less stable, give longer pause
        private int ttlWaitMlutiplier = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 8 : 2;

        private List<ItemRemovedEventArgs<int, int>> removedItems = new List<ItemRemovedEventArgs<int, int>>();
        private List<ItemUpdatedEventArgs<int, int>> updatedItems = new List<ItemUpdatedEventArgs<int, int>>();

        private void OnLfuItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            removedItems.Add(e);
        }

        private void OnLfuItemUpdated(object sender, ItemUpdatedEventArgs<int, int> e)
        {
            updatedItems.Add(e);
        }

        public ConcurrentTLfuTests()
        {
            lfu = new ConcurrentTLfu<int, string>(capacity, new ExpireAfterWrite<int, string>(timeToLive));
        }

        // This is a scenario test to verify maintenance is run promptly after read.
        [RetryFact]
        public void WhenItemIsAccessedTimeToExpireIsUpdated()
        {
            var cache = new ConcurrentLfuBuilder<int, int>()
                .WithCapacity(10)
                .WithExpireAfterAccess(TimeSpan.FromSeconds(5))
                .Build();

            Timed.Execute(
                cache,
                cache =>
                {
                    cache.AddOrUpdate(1, 1);
                    return cache;
                },
                TimeSpan.FromSeconds(4),
                cache =>
                {
                    cache.TryGet(1, out var value);
                },
                TimeSpan.FromSeconds(2),
                cache =>
                {
                    cache.TryGet(1, out var value).Should().BeTrue();
                    cache.TryGet(1, out value).Should().BeTrue();
                }
            );
        }

        [Fact]
        public void ConstructAddAndRetrieveWithCustomComparerReturnsValue()
        {
            var lfu = new ConcurrentTLfu<string, int>(9, 9, new NullScheduler(), StringComparer.OrdinalIgnoreCase, new ExpireAfterWrite<string, int>(timeToLive));

            lfu.GetOrAdd("foo", k => 1);

            lfu.TryGet("FOO", out var value).Should().BeTrue();
            value.Should().Be(1);
        }

        [Fact]
        public void MetricsHasValueIsTrue()
        {
            var x = new ConcurrentTLfu<int, int>(3, new TestExpiryCalculator<int, int>());
            x.Metrics.HasValue.Should().BeTrue();
        }

        [Fact]
        public void EventsAreEnabled()
        {
            var x = new ConcurrentTLfu<int, int>(3, new TestExpiryCalculator<int, int>());
            x.Events.HasValue.Should().BeTrue();
        }

        [Fact]
        public void DefaultSchedulerIsThreadPool()
        {
            lfu.Scheduler.Should().BeOfType<ThreadPoolScheduler>();
        }

        [Fact]
        public void WhenCalculatorIsAfterWritePolicyIsAfterWrite()
        {
            lfu.Policy.ExpireAfterWrite.HasValue.Should().BeTrue();
            lfu.Policy.ExpireAfterWrite.Value.TimeToLive.Should().Be(timeToLive);
        }

        [Fact]
        public void WhenCalculatorIsAfterAccessPolicyIsAfterAccess()
        {
            lfu = new ConcurrentTLfu<int, string>(capacity, new ExpireAfterAccess<int, string>(timeToLive));

            lfu.Policy.ExpireAfterAccess.HasValue.Should().BeTrue();
            lfu.Policy.ExpireAfterAccess.Value.TimeToLive.Should().Be(timeToLive);
        }

        [Fact]
        public void WhenCalculatorIsCustomPolicyIsAfter()
        {
            lfu = new ConcurrentTLfu<int, string>(capacity, new TestExpiryCalculator<int, string>());

            lfu.Policy.ExpireAfter.HasValue.Should().BeTrue();
            (lfu as ITimePolicy).TimeToLive.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void WhenKeyExistsTryGetTimeToExpireReturnsExpiry()
        {
            var calc = new TestExpiryCalculator<int, string>();
            calc.ExpireAfterCreate = (k, v) => Duration.FromMinutes(1);
            lfu = new ConcurrentTLfu<int, string>(capacity, calc);

            lfu.GetOrAdd(1, k => "1");

            lfu.Policy.ExpireAfter.Value.TryGetTimeToExpire(1, out var timeToExpire).Should().BeTrue();
            timeToExpire.Should().BeCloseTo(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public void WhenKeyDoesNotExistTryGetTimeToExpireReturnsFalse()
        {
            lfu = new ConcurrentTLfu<int, string>(capacity, new TestExpiryCalculator<int, string>());

            lfu.Policy.ExpireAfter.Value.TryGetTimeToExpire(1, out _).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyTypeMismatchTryGetTimeToExpireReturnsFalse()
        {
            lfu = new ConcurrentTLfu<int, string>(capacity, new TestExpiryCalculator<int, string>());

            lfu.Policy.ExpireAfter.Value.TryGetTimeToExpire("string", out _).Should().BeFalse();
        }

        // policy can expire after write

        [RetryFact]
        public void WhenItemIsNotExpiredItIsNotRemoved()
        {
            lfu.GetOrAdd(1, valueFactory.Create);

            lfu.TryGet(1, out var value).Should().BeTrue();
        }

        [RetryFact]
        public void WhenItemIsExpiredItIsRemoved()
        {
            Timed.Execute(
                lfu,
                lfu =>
                {
                    lfu.GetOrAdd(1, valueFactory.Create);
                    return lfu;
                },
                timeToLive.MultiplyBy(ttlWaitMlutiplier),
                lfu =>
                {
                    lfu.TryGet(1, out var value).Should().BeFalse();
                }
            );
        }

        [RetryFact]
        public void WhenItemIsExpiredItIsRemoved2()
        {
            Timed.Execute(
                lfu,
                lfu =>
                {
                    lfu.GetOrAdd(1, valueFactory.Create);
                    return lfu;
                },
                TimeSpan.FromSeconds(2),
                lfu =>
                {
                    // This is a bit flaky below 2 secs pause - seems like it doesnt always
                    // remove the item
                    lfu.Policy.ExpireAfterWrite.Value.TrimExpired();
                    lfu.Count.Should().Be(0);
                }
            );
        }

        [RetryFact]
        public void WhenItemIsUpdatedTtlIsExtended()
        {
            Timed.Execute(
                lfu,
                lfu =>
                {
                    lfu.GetOrAdd(1, valueFactory.Create);
                    return lfu;
                },
                timeToLive.MultiplyBy(ttlWaitMlutiplier),
                lfu =>
                {
                    lfu.TryUpdate(1, "3");

                    // If we defer computing time to the maintenance loop, we
                    // need to call maintenance here for the timestamp to be updated
                    lfu.DoMaintenance();
                    lfu.TryGet(1, out var value).Should().BeTrue();
                }
            );
        }

        [Fact]
        public void WhenItemIsRemovedRemovedEventIsFired()
        {
            removedItems.Clear();
            var lfuEvents = new ConcurrentTLfu<int, int>(20, new TestExpiryCalculator<int, int>());
            lfuEvents.Events.Value.ItemRemoved += OnLfuItemRemoved;

            lfuEvents.GetOrAdd(1, i => i + 2);

            lfuEvents.TryRemove(1).Should().BeTrue();

            // Maintenance is needed for events to be processed
            lfuEvents.DoMaintenance();

            removedItems.Count.Should().Be(1);
            removedItems[0].Key.Should().Be(1);
            removedItems[0].Value.Should().Be(3);
            removedItems[0].Reason.Should().Be(ItemRemovedReason.Removed);
        }

        [Fact]
        public void WhenItemRemovedEventIsUnregisteredEventIsNotFired()
        {
            removedItems.Clear();
            var lfuEvents = new ConcurrentTLfu<int, int>(20, new TestExpiryCalculator<int, int>());

            lfuEvents.Events.Value.ItemRemoved += OnLfuItemRemoved;
            lfuEvents.Events.Value.ItemRemoved -= OnLfuItemRemoved;

            lfuEvents.GetOrAdd(1, i => i + 1);
            lfuEvents.TryRemove(1);
            lfuEvents.DoMaintenance();

            removedItems.Count.Should().Be(0);
        }

        [Fact]
        public void WhenValueEvictedItemRemovedEventIsFired()
        {
            removedItems.Clear();
            var lfuEvents = new ConcurrentTLfu<int, int>(6, new TestExpiryCalculator<int, int>());
            lfuEvents.Events.Value.ItemRemoved += OnLfuItemRemoved;

            // Fill cache to capacity
            for (int i = 0; i < 6; i++)
            {
                lfuEvents.GetOrAdd(i, i => i);
            }

            // This should trigger eviction
            lfuEvents.GetOrAdd(100, i => i);
            lfuEvents.DoMaintenance();

            // At least one item should be evicted
            removedItems.Count.Should().BeGreaterThan(0);
            removedItems.Any(r => r.Reason == ItemRemovedReason.Evicted).Should().BeTrue();
        }

        [Fact]
        public void WhenItemsAreTrimmedAnEventIsFired()
        {
            removedItems.Clear();
            var lfuEvents = new ConcurrentTLfu<int, int>(20, new TestExpiryCalculator<int, int>());
            lfuEvents.Events.Value.ItemRemoved += OnLfuItemRemoved;

            for (int i = 0; i < 6; i++)
            {
                lfuEvents.GetOrAdd(i, i => i);
            }

            lfuEvents.Trim(2);

            removedItems.Count.Should().Be(2);
            removedItems.All(r => r.Reason == ItemRemovedReason.Trimmed).Should().BeTrue();
        }

        [Fact]
        public void WhenItemsAreClearedAnEventIsFired()
        {
            removedItems.Clear();
            var lfuEvents = new ConcurrentTLfu<int, int>(20, new TestExpiryCalculator<int, int>());
            lfuEvents.Events.Value.ItemRemoved += OnLfuItemRemoved;

            for (int i = 0; i < 6; i++)
            {
                lfuEvents.GetOrAdd(i, i => i);
            }

            lfuEvents.Clear();

            removedItems.Count.Should().Be(6);
            removedItems.All(r => r.Reason == ItemRemovedReason.Cleared).Should().BeTrue();
        }

        // backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenItemExistsAddOrUpdateFiresUpdateEvent()
        {
            updatedItems.Clear();
            var lfuEvents = new ConcurrentTLfu<int, int>(20, new TestExpiryCalculator<int, int>());
            lfuEvents.Events.Value.ItemUpdated += OnLfuItemUpdated;

            lfuEvents.AddOrUpdate(1, 2);
            lfuEvents.AddOrUpdate(2, 3);

            lfuEvents.AddOrUpdate(1, 3);

            updatedItems.Count.Should().Be(1);
            updatedItems[0].Key.Should().Be(1);
            updatedItems[0].OldValue.Should().Be(2);
            updatedItems[0].NewValue.Should().Be(3);
        }

        [Fact]
        public void WhenItemExistsTryUpdateFiresUpdateEvent()
        {
            updatedItems.Clear();
            var lfuEvents = new ConcurrentTLfu<int, int>(20, new TestExpiryCalculator<int, int>());
            lfuEvents.Events.Value.ItemUpdated += OnLfuItemUpdated;

            lfuEvents.AddOrUpdate(1, 2);
            lfuEvents.AddOrUpdate(2, 3);

            lfuEvents.TryUpdate(1, 3);

            updatedItems.Count.Should().Be(1);
            updatedItems[0].Key.Should().Be(1);
            updatedItems[0].OldValue.Should().Be(2);
            updatedItems[0].NewValue.Should().Be(3);
        }

        [Fact]
        public void WhenItemUpdatedEventIsUnregisteredEventIsNotFired()
        {
            updatedItems.Clear();
            var lfuEvents = new ConcurrentTLfu<int, int>(20, new TestExpiryCalculator<int, int>());

            lfuEvents.Events.Value.ItemUpdated += OnLfuItemUpdated;
            lfuEvents.Events.Value.ItemUpdated -= OnLfuItemUpdated;

            lfuEvents.AddOrUpdate(1, 2);
            lfuEvents.AddOrUpdate(1, 2);
            lfuEvents.AddOrUpdate(1, 2);

            updatedItems.Count.Should().Be(0);
        }
#endif
    }
}
