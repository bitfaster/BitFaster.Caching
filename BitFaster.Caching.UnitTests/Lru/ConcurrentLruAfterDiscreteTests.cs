using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentLruAfterDiscreteTests
    {
        private readonly ICapacityPartition capacity = new EqualCapacityPartition(9);
        private ICache<int, string> lru;

        private ValueFactory valueFactory = new ValueFactory();
        private TestExpiryCalculator<int, string> expiryCalculator = new TestExpiryCalculator<int, string>();

        private List<ItemRemovedEventArgs<int, string>> removedItems = new List<ItemRemovedEventArgs<int, string>>();

        // on MacOS time measurement seems to be less stable, give longer pause
        private int ttlWaitMlutiplier = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 8 : 2;

        private static readonly TimeSpan delta = TimeSpan.FromMilliseconds(20);

        private void OnLruItemRemoved(object sender, ItemRemovedEventArgs<int, string> e)
        {
            removedItems.Add(e);
        }

        public ConcurrentLruAfterDiscreteTests()
        {
            lru = new ConcurrentLruBuilder<int, string>()
                .WithCapacity(capacity)
                .WithExpireAfter(expiryCalculator)
                .Build();
        }

        [Fact]
        public void WhenKeyIsWrongTypeTryGetTimeToExpireIsFalse()
        {
            lru.Policy.ExpireAfter.Value.TryGetTimeToExpire("foo", out _).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyDoesNotExistTryGetTimeToExpireIsFalse()
        {
            lru.Policy.ExpireAfter.Value.TryGetTimeToExpire(1, out _).Should().BeFalse();
        }

        [Fact]
        public void WhenKeyExistsTryGetTimeToExpireReturnsExpiryTime()
        {
            lru.GetOrAdd(1, k => "1");
            lru.Policy.ExpireAfter.Value.TryGetTimeToExpire(1, out var expiry).Should().BeTrue();
            expiry.Should().BeCloseTo(TestExpiryCalculator<int, string>.DefaultTimeToExpire.ToTimeSpan(), delta);
        }

        [Fact]
        public void WhenItemIsExpiredItIsRemoved()
        {
            Timed.Execute(
                lru,
                lru =>
                {
                    lru.GetOrAdd(1, valueFactory.Create);
                    return lru;
                },
                TestExpiryCalculator<int, string>.DefaultTimeToExpire.MultiplyBy(ttlWaitMlutiplier),
                lru =>
                {
                    lru.TryGet(1, out var value).Should().BeFalse();
                }
            );
        }

        [Fact]
        public void WhenItemIsUpdatedTtlIsExtended()
        {
            Timed.Execute(
                lru,
                lru =>
                {
                    lru.GetOrAdd(1, valueFactory.Create);
                    return lru;
                },
                TestExpiryCalculator<int, string>.DefaultTimeToExpire.MultiplyBy(ttlWaitMlutiplier),
                lru =>
                {
                    lru.TryUpdate(1, "3");
                    lru.TryGet(1, out var value).Should().BeTrue();
                }
            );
        }

        [Fact]
        public void WhenItemIsReadTtlIsExtended()
        {
            expiryCalculator.ExpireAfterCreate = (_, _) => Duration.FromMilliseconds(100);

            var lru = new ConcurrentLruBuilder<int, string>()
                        .WithCapacity(capacity)
                        .WithExpireAfter(expiryCalculator)
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
                    lru.TryGet(1, out _).Should().BeTrue($"First");
                },
                TimeSpan.FromMilliseconds(75),
                lru =>
                {
                    lru.TryGet(1, out var value).Should().BeTrue($"Second");
                }
            );
        }

        [Fact]
        public void WhenValueEvictedItemRemovedEventIsFired()
        {
            expiryCalculator.ExpireAfterCreate = (_, _) => Duration.FromSeconds(10);

            var lruEvents = new ConcurrentLruBuilder<int, string>()
                .WithCapacity(new EqualCapacityPartition(6))
                .WithExpireAfter(expiryCalculator)
                .WithMetrics()
                .Build();

            lruEvents.Events.Value.ItemRemoved += OnLruItemRemoved;

            // First 6 adds
            // hot[6, 5], warm[2, 1], cold[4, 3]
            // =>
            // hot[8, 7], warm[1, 0], cold[6, 5], evicted[4, 3]
            for (int i = 0; i < 8; i++)
            {
                lruEvents.GetOrAdd(i + 1, i => $"{i + 1}");
            }

            removedItems.Count.Should().Be(2);

            removedItems[0].Key.Should().Be(1);
            removedItems[0].Value.Should().Be("2");
            removedItems[0].Reason.Should().Be(ItemRemovedReason.Evicted);

            removedItems[1].Key.Should().Be(4);
            removedItems[1].Value.Should().Be("5");
            removedItems[1].Reason.Should().Be(ItemRemovedReason.Evicted);
        }

        [Fact]
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
                TestExpiryCalculator<int, string>.DefaultTimeToExpire.MultiplyBy(ttlWaitMlutiplier),
                lru =>
                {
                    lru.Policy.ExpireAfter.Value.TrimExpired();

                    lru.Count.Should().Be(0);
                }
            );
        }

        [Fact]
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
              TestExpiryCalculator<int, string>.DefaultTimeToExpire.MultiplyBy(ttlWaitMlutiplier),
              lru =>
              {
                  lru.GetOrAdd(1, valueFactory.Create);
                  lru.GetOrAdd(2, valueFactory.Create);
                  lru.GetOrAdd(3, valueFactory.Create);

                  lru.Policy.ExpireAfter.Value.TrimExpired();

                  lru.Count.Should().Be(3);
              }
          );
        }

        [Fact]
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
                TestExpiryCalculator<int, string>.DefaultTimeToExpire.MultiplyBy(ttlWaitMlutiplier),
                lru =>
                {
                    lru.Policy.Eviction.Value.Trim(1);

                    lru.Count.Should().Be(0);
                }
            );
        }
    }
}
