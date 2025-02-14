using Shouldly;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using Xunit;
using System.Runtime.InteropServices;
using BitFaster.Caching.UnitTests.Retry;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentTLruTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly TimeSpan timeToLive = TimeSpan.FromMilliseconds(10);
        private readonly ICapacityPartition capacity = new EqualCapacityPartition(9);
        private ConcurrentTLru<int, string> lru;

        private ValueFactory valueFactory = new ValueFactory();

        private List<ItemRemovedEventArgs<int, int>> removedItems = new List<ItemRemovedEventArgs<int, int>>();

        // on MacOS time measurement seems to be less stable, give longer pause
        private int ttlWaitMlutiplier = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 8 : 2;

        private void OnLruItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            removedItems.Add(e);
        }

        public ConcurrentTLru<K, V> CreateTLru<K, V>(ICapacityPartition capacity, TimeSpan timeToLive)
        {
            return new ConcurrentTLru<K, V>(1, capacity, EqualityComparer<K>.Default, timeToLive);
        }

        public ConcurrentTLruTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
            lru = CreateTLru<int, string>(capacity, timeToLive);
        }

        [Fact]
        public void CanExpireIsTrue()
        {
            this.lru.Policy.ExpireAfterWrite.HasValue.ShouldBeTrue();
        }

        [Fact]
        public void TimeToLiveIsCtorArg()
        {
            this.lru.Policy.ExpireAfterWrite.Value.TimeToLive.ShouldBe(timeToLive);
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

        [Fact]
        public void WhenValueEvictedItemRemovedEventIsFired()
        {
            var lruEvents = CreateTLru<int, int>(new EqualCapacityPartition(6), TimeSpan.FromSeconds(10));
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

        [Fact]
        public void WhenItemRemovedEventIsUnregisteredEventIsNotFired()
        {
            var lruEvents = CreateTLru<int, int>(new EqualCapacityPartition(6), timeToLive);
            lruEvents.Events.Value.ItemRemoved += OnLruItemRemoved;
            lruEvents.Events.Value.ItemRemoved -= OnLruItemRemoved;

            for (int i = 0; i < 6; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => i + 1);
            }

            removedItems.Count.ShouldBe(0);
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
                    lru.Policy.ExpireAfterWrite.Value.TrimExpired();

                    lru.HotCount.ShouldBe(0);
                    lru.WarmCount.ShouldBe(0);
                    lru.ColdCount.ShouldBe(0);
                }
            );
        }

        [RetryFact]
        public void WhenExpiredItemsAreTrimmedCacheMarkedCold()
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
                    lru.Policy.ExpireAfterWrite.Value.TrimExpired();

                    for (int i = 0; i < lru.Policy.Eviction.Value.Capacity; i++)
                    {
                        lru.GetOrAdd(i, k => k.ToString());
                    }

                    lru.Count.ShouldBe(lru.Policy.Eviction.Value.Capacity);

                    var total = lru.HotCount + lru.WarmCount + lru.ColdCount;
                    total.ShouldBe(lru.Policy.Eviction.Value.Capacity);
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

                  lru.Policy.ExpireAfterWrite.Value.TrimExpired();

                  lru.Count.ShouldBe(3);

                  var total = lru.HotCount + lru.WarmCount + lru.ColdCount;
                  total.ShouldBe(3);
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

                    lru.HotCount.ShouldBe(0);
                    lru.WarmCount.ShouldBe(0);
                    lru.ColdCount.ShouldBe(0);
                }
            );
        }

        [RetryFact]
        public void WhenItemsAreExpiredCountFiltersExpiredItems()
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
                    lru.Count.ShouldBe(0);
                }
            );
        }

        [RetryFact]
        public void WhenItemsAreExpiredEnumerateFiltersExpiredItems()
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
                    lru.ShouldBe(Array.Empty<KeyValuePair<int, string>>());
                }
            );
        }

        [Fact]
        public void WhenItemsAreRemovedTrimExpiredRemovesDeletedItemsFromQueues()
        {
            lru = CreateTLru<int, string>(capacity, TimeSpan.FromMinutes(1));

            for (int i = 0; i < lru.Capacity; i++)
            {
                lru.GetOrAdd(i, valueFactory.Create);
            }

            Print();                  // Hot [6,7,8] Warm [1,2,3] Cold [0,4,5]

            lru.TryRemove(0);
            lru.TryRemove(1);
            lru.TryRemove(6);

            lru.Policy.ExpireAfterWrite.Value.TrimExpired();

            Print();                  // Hot [7,8] Warm [2,3] Cold [4,5]

            lru.HotCount.Should().Be(2);
            lru.WarmCount.Should().Be(2);
            lru.ColdCount.Should().Be(2);
        }

        [Fact]
        public void ConstructWithDefaultCtorReturnsCapacity()
        {
            var x = new ConcurrentTLru<int, int>(3, TimeSpan.FromSeconds(1));

            x.Capacity.ShouldBe(3);
        }

        [Fact]
        public void ConstructCapacityCtorReturnsCapacity()
        {
            var x = new ConcurrentTLru<int, int>(1, 3, EqualityComparer<int>.Default, TimeSpan.FromSeconds(1));

            x.Capacity.ShouldBe(3);
        }

        [Fact]
        public void ConstructPartitionCtorReturnsCapacity()
        {
            var x = new ConcurrentTLru<int, int>(1, new EqualCapacityPartition(3), EqualityComparer<int>.Default, TimeSpan.FromSeconds(1));

            x.Capacity.ShouldBe(3);
        }

        private void Print()
        {
#if DEBUG
            this.testOutputHelper.WriteLine(this.lru.FormatLruString());
#endif
        }
    }
}
