using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public abstract class ConcurrentLfuCoreTests
    {
        protected readonly TimeSpan timeToLive = TimeSpan.FromMilliseconds(200);
        protected readonly int capacity = 20;

        private ConcurrentLfuTests.ValueFactory valueFactory = new();

        private ICache<int, int> lfu;

        public abstract ICache<K, V> Create<K,V>();
        public abstract void DoMaintenance<K, V>(ICache<K, V> cache);

        public ConcurrentLfuCoreTests()
        {
            lfu = Create<int, int>();
        }

        [Fact]
        public void EvictionPolicyCapacityReturnsCapacity()
        {
            lfu.Policy.Eviction.Value.Capacity.Should().Be(capacity);
        }

        [Fact]
        public void WhenKeyIsRequestedItIsCreatedAndCached()
        {
            var result1 = lfu.GetOrAdd(1, valueFactory.Create);
            var result2 = lfu.GetOrAdd(1, valueFactory.Create);

            valueFactory.timesCalled.Should().Be(1);
            result1.Should().Be(result2);
        }
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenKeyIsRequestedWithArgItIsCreatedAndCached()
        {
            var result1 = lfu.GetOrAdd(1, valueFactory.Create, 9);
            var result2 = lfu.GetOrAdd(1, valueFactory.Create, 17);

            valueFactory.timesCalled.Should().Be(1);
            result1.Should().Be(result2);
        }
#endif
        [Fact]
        public async Task WhenKeyIsRequesteItIsCreatedAndCachedAsync()
        {
            var asyncLfu = lfu as IAsyncCache<int, int>;
            var result1 = await asyncLfu.GetOrAddAsync(1, valueFactory.CreateAsync);
            var result2 = await asyncLfu.GetOrAddAsync(1, valueFactory.CreateAsync);

            valueFactory.timesCalled.Should().Be(1);
            result1.Should().Be(result2);
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public async Task WhenKeyIsRequestedWithArgItIsCreatedAndCachedAsync()
        {
            var asyncLfu = lfu as IAsyncCache<int, int>;
            var result1 = await asyncLfu.GetOrAddAsync(1, valueFactory.CreateAsync, 9);
            var result2 = await asyncLfu.GetOrAddAsync(1, valueFactory.CreateAsync, 17);

            valueFactory.timesCalled.Should().Be(1);
            result1.Should().Be(result2);
        }
#endif

        [Fact]
        public void WhenItemIsUpdatedItIsUpdated()
        {
            lfu.GetOrAdd(1, k => k);
            lfu.AddOrUpdate(1, 2);

            lfu.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(2);
        }

        [Fact]
        public void WhenItemDoesNotExistUpdatedAddsItem()
        {
            lfu.AddOrUpdate(1, 2);

            lfu.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(2);
        }


        [Fact]
        public void WhenKeyExistsTryRemoveRemovesItem()
        {
            lfu.GetOrAdd(1, k => k);

            lfu.TryRemove(1).Should().BeTrue();
            lfu.TryGet(1, out _).Should().BeFalse();
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenKeyExistsTryRemoveReturnsValue()
        {
            lfu.GetOrAdd(1, valueFactory.Create);

            lfu.TryRemove(1, out var value).Should().BeTrue();
            value.Should().Be(1);
        }

        [Fact]
        public void WhenItemExistsTryRemoveRemovesItem()
        {
            lfu.GetOrAdd(1, k => k);

            lfu.TryRemove(new KeyValuePair<int, int>(1, 1)).Should().BeTrue();
            lfu.TryGet(1, out _).Should().BeFalse();
        }

        [Fact]
        public void WhenItemDoesntMatchTryRemoveDoesNotRemove()
        {
            lfu.GetOrAdd(1, k => k);

            lfu.TryRemove(new KeyValuePair<int, int>(1, 2)).Should().BeFalse();
            lfu.TryGet(1, out var value).Should().BeTrue();
        }
#endif
 
        [Fact]
        public void WhenClearedCacheIsEmpty()
        {
            lfu.GetOrAdd(1, k => k);
            lfu.GetOrAdd(2, k => k);

            lfu.Clear();

            lfu.Count.Should().Be(0);
            lfu.TryGet(1, out var _).Should().BeFalse();
        }

        [Fact]
        public void TrimRemovesNItems()
        {
            for (int i = 0; i < 25; i++)
            {
                lfu.GetOrAdd(i, k => k);
            }
            DoMaintenance<int, int>(lfu);

            lfu.Count.Should().Be(20);

            lfu.Policy.Eviction.Value.Trim(5);
            DoMaintenance<int, int>(lfu);

            lfu.Count.Should().Be(15);
        }

        [Fact]
        public void WhenItemsAddedGenericEnumerateContainsKvps()
        {
            lfu.GetOrAdd(1, k => k);
            lfu.GetOrAdd(2, k => k);

            var enumerator = lfu.GetEnumerator();
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be(new KeyValuePair<int, int>(1, 1));
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be(new KeyValuePair<int, int>(2, 2));
        }

        [Fact]
        public void WhenItemsAddedEnumerateContainsKvps()
        {
            lfu.GetOrAdd(1, k => k);
            lfu.GetOrAdd(2, k => k);

            var enumerable = (IEnumerable)lfu;
            enumerable.Should().BeEquivalentTo(new[] { new KeyValuePair<int, int>(1, 1), new KeyValuePair<int, int>(2, 2) });
        }
    }

    public class ConcurrentTLfuWrapperTests : ConcurrentLfuCoreTests
    {
        public override ICache<K, V> Create<K,V>()
        {
            return new ConcurrentTLfu<K, V>(capacity, new ExpireAfterWrite<K, V>(timeToLive));
        }

        public override void DoMaintenance<K, V>(ICache<K, V> cache)
        {
            var tlfu = cache as ConcurrentTLfu<K, V>;
            tlfu?.DoMaintenance();
        }
    }
}
