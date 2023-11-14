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
        private readonly TimeSpan defaultTimeToExpire = TimeSpan.FromMinutes(1);
        private readonly ICapacityPartition capacity = new EqualCapacityPartition(9);
        private ICache<int, string> lru;

        private ValueFactory valueFactory = new ValueFactory();
        private TestExpiryCalculator<int, string> expiryCalculator = new TestExpiryCalculator<int, string>();

        private List<ItemRemovedEventArgs<int, int>> removedItems = new List<ItemRemovedEventArgs<int, int>>();

        // on MacOS time measurement seems to be less stable, give longer pause
        private int ttlWaitMlutiplier = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 8 : 2;

        private static readonly TimeSpan delta = TimeSpan.FromMilliseconds(20);


        private void OnLruItemRemoved(object sender, ItemRemovedEventArgs<int, int> e)
        {
            removedItems.Add(e);
        }

        public ConcurrentLruAfterDiscreteTests()
        {
            expiryCalculator.ExpireAfterCreate = (_, _) => defaultTimeToExpire;
            expiryCalculator.ExpireAfterRead = (_, _, _) => defaultTimeToExpire;
            expiryCalculator.ExpireAfterUpdate = (_, _, _) => defaultTimeToExpire;

            lru = new ConcurrentLruBuilder<int, string>()
                .WithCapacity(capacity)
                .WithExpiry(expiryCalculator)
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
            expiry.Should().BeCloseTo(defaultTimeToExpire, delta);
        }
    }
}
