using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BitFaster.Caching.Lru;
using BitFaster.Caching.UnitTests.Retry;
using Shouldly;
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
            this.lru.Policy.ExpireAfterAccess.HasValue.ShouldBeTrue();
        }

        [Fact]
        public void TimeToLiveIsCtorArg()
        {
            this.lru.Policy.ExpireAfterAccess.Value.TimeToLive.ShouldBe(timeToLive);
        }

        [RetryFact]
        public void WhenItemIsNotExpiredItIsNotRemoved()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            lru.TryGet(1, out var value).ShouldBeTrue();
        }

        [RetryFact]
        public void WhenItemIsExpiredItIsRemoved()
        {
            Timed.Execute(
                lru,
                lru =>
                {
                    lru.GetOrAdd(1, valueFactory.Create);
                    return lru;
                },
                timeToLive.MultiplyBy(ttlWaitMlutiplier),
                lru =>
                {
                    lru.TryGet(1, out var value).ShouldBeFalse();
                }
            );
        }

        [RetryFact]
        public void WhenItemIsUpdatedTtlIsExtended()
        {
            Timed.Execute(
                lru,
                lru =>
                {
                    lru.GetOrAdd(1, valueFactory.Create);
                    return lru;
                },
                timeToLive.MultiplyBy(ttlWaitMlutiplier),
                lru =>
                {
                    lru.TryUpdate(1, "3");
                    lru.TryGet(1, out var value).ShouldBeTrue();
                }
            );
        }

        // Using async/await makes this very unstable due to xunit
        // running new tests on the yielding thread. Using sleep
        // forces the test to stay on the same thread.
        [RetryFact]
        public void WhenItemIsReadTtlIsExtended()
        {
            var lru = new ConcurrentLruBuilder<int, string>()
                        .WithCapacity(capacity)
                        .WithExpireAfterAccess(TimeSpan.FromMilliseconds(100))
                        .Build();

            // execute the method to ensure it is always jitted
            lru.GetOrAdd(-1, valueFactory.Create);
            lru.GetOrAdd(-2, valueFactory.Create);
            lru.GetOrAdd(-3, valueFactory.Create);

            Timed.Execute(
                lru,
                lru =>
                {
                    

                    lru.GetOrAdd(1, valueFactory.Create);

                    return lru;
                },
                TimeSpan.FromMilliseconds(50),
                lru =>
                {
                    lru.TryGet(1, out _).ShouldBeTrue($"First");
                }, 
                TimeSpan.FromMilliseconds(75),
                lru =>
                {
                    lru.TryGet(1, out var value).ShouldBeTrue($"Second");
                }
            );
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

            removedItems.Count.ShouldBe(2);

            removedItems[0].Key.ShouldBe(1);
            removedItems[0].Value.ShouldBe(2);
            removedItems[0].Reason.ShouldBe(ItemRemovedReason.Evicted);

            removedItems[1].Key.ShouldBe(4);
            removedItems[1].Value.ShouldBe(5);
            removedItems[1].Reason.ShouldBe(ItemRemovedReason.Evicted);
        }

        [RetryFact]
        public void WhenItemsAreExpiredExpireRemovesExpiredItems()
        {
            Timed.Execute(
                lru,
                lru =>
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

                    return lru;
                },
                timeToLive.MultiplyBy(ttlWaitMlutiplier),
                lru => 
                {
                    lru.Policy.ExpireAfterAccess.Value.TrimExpired();

                    lru.Count.ShouldBe(0);
                }
            );
        }

        [RetryFact]
        public void WhenCacheHasExpiredAndFreshItemsExpireRemovesOnlyExpiredItems()
        {
            Timed.Execute(
              lru,
              lru =>
              {
                  lru.AddOrUpdate(1, "1");
                  lru.AddOrUpdate(2, "2");
                  lru.AddOrUpdate(3, "3");

                  lru.AddOrUpdate(4, "4");
                  lru.AddOrUpdate(5, "5");
                  lru.AddOrUpdate(6, "6");

                  return lru;
              },
              timeToLive.MultiplyBy(ttlWaitMlutiplier),
              lru =>
              {
                  lru.GetOrAdd(1, valueFactory.Create);
                  lru.GetOrAdd(2, valueFactory.Create);
                  lru.GetOrAdd(3, valueFactory.Create);

                  lru.Policy.ExpireAfterAccess.Value.TrimExpired();

                  lru.Count.ShouldBe(3);
              }
          );
        }

        [RetryFact]
        public void WhenItemsAreExpiredTrimRemovesExpiredItems()
        {
            Timed.Execute(
                lru,
                lru =>
                {
                    lru.AddOrUpdate(1, "1");
                    lru.AddOrUpdate(2, "2");
                    lru.AddOrUpdate(3, "3");

                    return lru;
                },
                timeToLive.MultiplyBy(ttlWaitMlutiplier),
                lru =>
                {
                    lru.Policy.Eviction.Value.Trim(1);

                    lru.Count.ShouldBe(0);
                }
            );
        }
    }
}
