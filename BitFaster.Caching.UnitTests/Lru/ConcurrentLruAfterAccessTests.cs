using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentLruAfterAccessTests
    {
        private readonly TimeSpan timeToLive = TimeSpan.FromMilliseconds(10);
        private readonly ICapacityPartition capacity = new EqualCapacityPartition(9);
        private ICache<int, string> lru;

        private ValueFactory valueFactory = new ValueFactory();

        private List<ItemRemovedEventArgs<int, int>> removedItems = new List<ItemRemovedEventArgs<int, int>>();

        // on MacOS time measurement seems to be less stable, give longer pause
        private int ttlWaitMlutiplier = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 8 : 2;

        private void OnLruItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            removedItems.Add(e);
        }

        public ConcurrentLruAfterAccessTests()
        {
            lru = new ConcurrentLruBuilder<int, string>()
                .WithCapacity(capacity)
                .WithExpireAfterAccess(timeToLive)
                .Build();
        }

        [Fact]
        public void CanExpireIsTrue()
        {
            this.lru.Policy.ExpireAfterAccess.HasValue.Should().BeTrue();
        }

        [Fact]
        public void TimeToLiveIsCtorArg()
        {
            this.lru.Policy.ExpireAfterAccess.Value.TimeToLive.Should().Be(timeToLive);
        }

        [Fact]
        public void WhenItemIsNotExpiredItIsNotRemoved()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryGet(1, out var value).Should().BeTrue();
        }

        [Fact]
        public async Task WhenItemIsExpiredItIsRemoved()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            await Task.Delay(timeToLive.MultiplyBy(ttlWaitMlutiplier));

            lru.TryGet(1, out var value).Should().BeFalse();
        }

        [Fact]
        public async Task WhenItemIsUpdatedTtlIsExtended()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            await Task.Delay(timeToLive.MultiplyBy(ttlWaitMlutiplier));

            lru.TryUpdate(1, "3");

            lru.TryGet(1, out var value).Should().BeTrue();
        }

        // Using async/await makes this very unstable due to xunit
        // running new tests on the yielding thread. Using sleep
        // forces the test to stay on the same thread.
        [Fact]
        public void WhenItemIsReadTtlIsExtended()
        {
            int attempts = 0;
            while (true)
            {
                var sw = Stopwatch.StartNew();

                lru = new ConcurrentLruBuilder<int, string>()
                    .WithCapacity(capacity)
                    .WithExpireAfterAccess(TimeSpan.FromMilliseconds(100))
                    .Build();

                lru.GetOrAdd(1, valueFactory.Create);

                Thread.Sleep(50);

                if (sw.Elapsed < TimeSpan.FromMilliseconds(75))
                {
                    lru.TryGet(1, out _).Should().BeTrue($"First {sw.Elapsed}");

                    Thread.Sleep(75);

                    if (sw.Elapsed < TimeSpan.FromMilliseconds(150))
                    {
                        lru.TryGet(1, out var value).Should().BeTrue($"Second {sw.Elapsed}");
                        break;
                    }
                }

                Thread.Sleep(200);
                attempts++.Should().BeLessThan(128, "Unable to run test within verification margin");
            }
        }

        [Fact]
        public void WhenValueEvictedItemRemovedEventIsFired()
        {
            var lruEvents = new ConcurrentLruBuilder<int, int>()
                .WithCapacity(new EqualCapacityPartition(6))
                .WithExpireAfterAccess(TimeSpan.FromSeconds(10))
                .WithMetrics()
                .Build();

            lruEvents.Events.Value.ItemRemoved += OnLruItemRemoved;

            // First 6 adds
            // hot[6, 5], warm[2, 1], cold[4, 3]
            // =>
            // hot[8, 7], warm[1, 0], cold[6, 5], evicted[4, 3]
            for (int i = 0; i < 8; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => i + 1);
            }

            removedItems.Count.Should().Be(2);

            removedItems[0].Key.Should().Be(1);
            removedItems[0].Value.Should().Be(2);
            removedItems[0].Reason.Should().Be(ItemRemovedReason.Evicted);

            removedItems[1].Key.Should().Be(4);
            removedItems[1].Value.Should().Be(5);
            removedItems[1].Reason.Should().Be(ItemRemovedReason.Evicted);
        }

        [Fact]
        public async Task WhenItemsAreExpiredExpireRemovesExpiredItems()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");
            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            lru.AddOrUpdate(4, "4");
            lru.AddOrUpdate(5, "5");
            lru.AddOrUpdate(6, "6");

            lru.AddOrUpdate(7, "7");
            lru.AddOrUpdate(8, "8");
            lru.AddOrUpdate(9, "9");

            await Task.Delay(timeToLive.MultiplyBy(ttlWaitMlutiplier));

            lru.Policy.ExpireAfterAccess.Value.TrimExpired();

            lru.Count.Should().Be(0);
        }

        [Fact]
        public async Task WhenCacheHasExpiredAndFreshItemsExpireRemovesOnlyExpiredItems()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");

            lru.AddOrUpdate(4, "4");
            lru.AddOrUpdate(5, "5");
            lru.AddOrUpdate(6, "6");

            await Task.Delay(timeToLive.MultiplyBy(ttlWaitMlutiplier));

            lru.GetOrAdd(1, valueFactory.Create);
            lru.GetOrAdd(2, valueFactory.Create);
            lru.GetOrAdd(3, valueFactory.Create);

            lru.Policy.ExpireAfterAccess.Value.TrimExpired();

            lru.Count.Should().Be(3);
        }

        [Fact]
        public async Task WhenItemsAreExpiredTrimRemovesExpiredItems()
        {
            lru.AddOrUpdate(1, "1");
            lru.AddOrUpdate(2, "2");
            lru.AddOrUpdate(3, "3");

            await Task.Delay(timeToLive.MultiplyBy(ttlWaitMlutiplier));

            lru.Policy.Eviction.Value.Trim(1);

            lru.Count.Should().Be(0);
        }
    }
}
