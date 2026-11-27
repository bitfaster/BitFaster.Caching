using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using BitFaster.Caching.UnitTests.Retry;
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

        public abstract ICache<K, V> Create<K, V>();
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

        [RetryFact]
        public void WhenItemIsUpdatedItIsUpdated()
        {
            lfu.GetOrAdd(1, k => k);
            lfu.AddOrUpdate(1, 2);

            lfu.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(2);
        }

        [RetryFact]
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
        public override ICache<K, V> Create<K, V>()
        {
            return new ConcurrentTLfu<K, V>(capacity, new ExpireAfterWrite<K, V>(timeToLive));
        }

        public override void DoMaintenance<K, V>(ICache<K, V> cache)
        {
            var tlfu = cache as ConcurrentTLfu<K, V>;
            tlfu?.DoMaintenance();
        }
    }

    public class WeightedConcurrentLfuWrapperTests : ConcurrentLfuCoreTests
    {
        public override ICache<K, V> Create<K, V>()
        {
            return new ConcurrentLfuBuilder<K, V>()
                .WithCapacity(capacity)
                .WithScheduler(new NullScheduler())
                .WithWeightedEntries(new TestWeightCalculator<K, V>((k, v) => 1))
                .WithEvents()
                .Build();
        }

        public override void DoMaintenance<K, V>(ICache<K, V> cache)
        {
            ((WeightedConcurrentLfu<K, V, WeightedAccessOrderNode<K, V>, WeightedAccessOrderPolicy<K, V, EventPolicy<K, V>>>)cache).DoMaintenance();
        }

        [Fact]
        public void DoMaintenanceProtectedEntriesExceedCapacityEvictsLeastRecentlyUsedEntry()
        {
            var cache = new ConcurrentLfuBuilder<int, int>()
                .WithCapacity(10)
                .WithScheduler(new NullScheduler())
                .WithWeightedEntries(new TestWeightCalculator<int, int>((k, v) => v))
                .WithEvents()
                .Build();
            cache.AddOrUpdate(1, 2);
            cache.AddOrUpdate(2, 2);
            DoMaintenance(cache);
            cache.TryGet(1, out _);
            cache.TryGet(2, out _);
            DoMaintenance(cache);

            cache.TryUpdate(1, 6);
            cache.TryUpdate(2, 6);
            DoMaintenance(cache);

            cache.Keys.Should().ContainSingle().Which.Should().Be(2);
        }

        [Fact]
        public void AddOrUpdateBufferedWeightsAreEvictedByConfiguredScheduler()
        {
            var cache = (WeightedConcurrentLfu<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, EventPolicy<int, int>>>)
                new ConcurrentLfuBuilder<int, int>()
                    .WithCapacity(10)
                    .WithScheduler(new TestScheduler())
                    .WithWeightedEntries(new TestWeightCalculator<int, int>((k, v) => v))
                    .WithEvents()
                    .Build();
            cache.AddOrUpdate(1, 6);
            cache.AddOrUpdate(2, 6);
            cache.Count.Should().Be(2);

            var scheduler = (TestScheduler)cache.Scheduler;
            scheduler.Work.TryTake(out var maintenance).Should().BeTrue();
            maintenance();

            cache.Keys.Should().ContainSingle().Which.Should().Be(1);
            cache.Metrics.Value.Evicted.Should().Be(1);
            WeightedConcurrentLfuCoreTests.AssertWeightedState(cache.Core);
        }

        [Fact]
        public void TryRemoveUnsubscribedItemRemovedHandlerReceivesNoFurtherEvents()
        {
            var cache = Create<int, int>();
            cache.AddOrUpdate(1, 1);
            cache.AddOrUpdate(2, 2);
            DoMaintenance(cache);
            var removedItems = new List<ItemRemovedEventArgs<int, int>>();
            EventHandler<ItemRemovedEventArgs<int, int>> handler = (sender, args) => removedItems.Add(args);
            cache.Events.Value.ItemRemoved += handler;
            cache.TryRemove(1).Should().BeTrue();
            DoMaintenance(cache);

            cache.Events.Value.ItemRemoved -= handler;
            cache.TryRemove(2).Should().BeTrue();
            DoMaintenance(cache);

            removedItems.Should().ContainSingle().Which.Key.Should().Be(1);
            cache.Keys.Should().BeEmpty();
            cache.Metrics.Value.Evicted.Should().Be(2);
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void TryUpdateUnsubscribedItemUpdatedHandlerReceivesNoFurtherEvents()
        {
            var cache = Create<int, int>();
            cache.AddOrUpdate(1, 1);
            DoMaintenance(cache);
            var updatedItems = new List<ItemUpdatedEventArgs<int, int>>();
            EventHandler<ItemUpdatedEventArgs<int, int>> handler = (sender, args) => updatedItems.Add(args);
            cache.Events.Value.ItemUpdated += handler;
            cache.TryUpdate(1, 2).Should().BeTrue();

            cache.Events.Value.ItemUpdated -= handler;
            cache.TryUpdate(1, 3).Should().BeTrue();
            DoMaintenance(cache);

            var update = updatedItems.Should().ContainSingle().Which;
            update.OldValue.Should().Be(1);
            update.NewValue.Should().Be(2);
            cache.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(3);
        }
#endif

        [Fact]
        public void TryGetTimeToExpireWithoutExpirationReturnsFalseForCachedEntry()
        {
            var cache = Create<int, int>();
            cache.AddOrUpdate(1, 1);

            ((IDiscreteTimePolicy)cache).TryGetTimeToExpire(1, out var timeToExpire).Should().BeFalse();

            timeToExpire.Should().Be(TimeSpan.Zero);
        }

#if NET9_0_OR_GREATER
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AlternateLookupIncreasingWeightPastCapacityEvictsEntry(bool useTryGet)
        {
            var cache = CreateWeightedStringCache();
            IAlternateLookup<ReadOnlySpan<char>, string, int> lookup;
            if (useTryGet)
            {
                cache.TryGetAlternateLookup(out lookup).Should().BeTrue();
            }
            else
            {
                lookup = cache.GetAlternateLookup<ReadOnlySpan<char>>();
            }
            lookup.GetOrAdd("entry".AsSpan(), _ => 6).Should().Be(6);

            lookup.TryUpdate("ENTRY".AsSpan(), 11).Should().BeTrue();

            cache.Keys.Should().BeEmpty();
            cache.Metrics.Value.Evicted.Should().Be(1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AsyncAlternateLookupOversizedEntryReturnsValueWithoutEvictingExistingEntry(bool useTryGet)
        {
            var cache = CreateWeightedStringCache();
            cache.AddOrUpdate("retained", 6);
            IAsyncAlternateLookup<ReadOnlySpan<char>, string, int> lookup;
            if (useTryGet)
            {
                ((IAsyncCache<string, int>)cache).TryGetAsyncAlternateLookup(out lookup).Should().BeTrue();
            }
            else
            {
                lookup = ((IAsyncCache<string, int>)cache).GetAsyncAlternateLookup<ReadOnlySpan<char>>();
            }

            var value = await lookup.GetOrAddAsync("oversized".AsSpan(), (key, weight) => Task.FromResult(weight), 11);

            value.Should().Be(11);
            cache.Keys.Should().ContainSingle().Which.Should().Be("retained");
            cache.Metrics.Value.Evicted.Should().Be(1);
        }

        private static ICache<string, int> CreateWeightedStringCache()
        {
            return new ConcurrentLfuBuilder<string, int>()
                .WithCapacity(10)
                .WithScheduler(new ForegroundScheduler())
                .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
                .WithWeightedEntries(new TestWeightCalculator<string, int>((k, v) => v))
                .WithEvents()
                .Build();
        }
#endif
    }

    public class WeightedConcurrentTLfuWrapperTests : ConcurrentLfuCoreTests
    {
        public override ICache<K, V> Create<K, V>()
        {
            return new ConcurrentLfuBuilder<K, V>()
                .WithCapacity(capacity)
                .WithScheduler(new NullScheduler())
                .WithWeightedEntries(new TestWeightCalculator<K, V>((k, v) => 1))
                .WithExpireAfterWrite(TimeSpan.FromMinutes(10))
                .WithEvents()
                .Build();
        }

        public override void DoMaintenance<K, V>(ICache<K, V> cache)
        {
            ((WeightedConcurrentLfu<K, V, WeightedTimeOrderNode<K, V>, WeightedExpireAfterPolicy<K, V, EventPolicy<K, V>>>)cache).DoMaintenance();
        }

        [Fact]
        public void TryGetTimeToExpireCachedEntryReturnsRemainingLifetime()
        {
            var configuredLifetime = TimeSpan.FromMinutes(10);
            var cache = CreateWithExpiry(new TestExpiryCalculator<int, int>((k, v) => Duration.FromTimeSpan(configuredLifetime)));
            var beforeCreate = Duration.SinceEpoch();
            cache.AddOrUpdate(1, 20);

            cache.Policy.ExpireAfter.Value.TryGetTimeToExpire(1, out var timeToExpire).Should().BeTrue();

            var elapsed = (Duration.SinceEpoch() - beforeCreate).ToTimeSpan();
            timeToExpire.Ticks.Should().BeInRange((configuredLifetime - elapsed).Ticks - 1, configuredLifetime.Ticks);
        }

        [Theory]
        [InlineData(2)]
        [InlineData("1")]
        [InlineData(null)]
        public void TryGetTimeToExpireMissingOrIncompatibleKeyReturnsNoExpiry(object key)
        {
            var cache = CreateWithExpiry(new TestExpiryCalculator<int, int>((k, v) => Duration.FromMinutes(10)));
            cache.AddOrUpdate(1, 20);

            cache.Policy.ExpireAfter.Value.TryGetTimeToExpire(key, out var timeToExpire).Should().BeFalse();

            timeToExpire.Should().Be(TimeSpan.Zero);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TrimExpiredPendingExpiredRemovalDiscountsWeightAndPreservesLiveEntries(bool customExpiry)
        {
            IExpiryCalculator<int, int> expiryCalculator = customExpiry
                ? new TestExpiryCalculator<int, int>((k, v) => Duration.FromMinutes(10))
                : new ExpireAfterWrite<int, int>(TimeSpan.FromMinutes(10));
            var cache = CreateWithExpiry(expiryCalculator);
            cache.AddOrUpdate(1, 20);
            cache.AddOrUpdate(2, 30);
            cache.DoMaintenance();
            cache.Core.TryGetNode(1, out var expiredNode);
            expiredNode.TimeToExpire = Duration.Zero;
            cache.TryGet(1, out _).Should().BeFalse();
            cache.Core.policy.eviction.WeightedSize.Should().Be(50);

            if (customExpiry)
            {
                cache.Policy.ExpireAfter.Value.TrimExpired();
            }
            else
            {
                cache.Policy.ExpireAfterWrite.Value.TrimExpired();
            }

            cache.Keys.Should().ContainSingle().Which.Should().Be(2);
            expiredNode.GetNextInTimeOrder().Should().BeNull();
            cache.Core.policy.eviction.WeightedSize.Should().Be(30);
            WeightedConcurrentLfuCoreTests.AssertWeightedState(cache.Core);
        }

        private static WeightedConcurrentLfu<int, int, WeightedTimeOrderNode<int, int>, WeightedExpireAfterPolicy<int, int, EventPolicy<int, int>>> CreateWithExpiry(IExpiryCalculator<int, int> expiryCalculator)
        {
            return (WeightedConcurrentLfu<int, int, WeightedTimeOrderNode<int, int>, WeightedExpireAfterPolicy<int, int, EventPolicy<int, int>>>)
                new ConcurrentLfuBuilder<int, int>()
                    .WithCapacity(100)
                    .WithScheduler(new NullScheduler())
                    .WithWeightedEntries(new TestWeightCalculator<int, int>((k, v) => v))
                    .WithExpireAfter(expiryCalculator)
                    .WithEvents()
                    .Build();
        }
    }

    public class WeightedConcurrentLfuCoreTests
    {
        [Fact]
        public void AddOrUpdateWithinWeightBudgetAccountsAllEntries()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 10);
            core.AddOrUpdate(2, 20);
            core.AddOrUpdate(3, 30);
            core.DoMaintenance();

            core.policy.eviction.WeightedSize.Should().Be(60);
            AssertWeightedState(core);
        }

        [Fact]
        public void AddOrUpdateBeyondWeightBudgetEvictsPositiveWeightEntries()
        {
            var core = CreateWeighted(10);
            for (int key = 0; key < 25; key++)
            {
                core.AddOrUpdate(key, 0);
            }
            for (int key = 25; key < 35; key++)
            {
                core.AddOrUpdate(key, 6);
            }
            core.DoMaintenance();

            core.Count.Should().BeGreaterThan(core.Capacity);
            for (int key = 0; key < 25; key++)
            {
                core.TryGet(key, out _).Should().BeTrue();
            }
            AssertWeightedState(core);
        }

        [Theory]
        [InlineData(101)]
        [InlineData(int.MaxValue)]
        public void AddOrUpdateOversizedEntryEvictsOnlyThatEntry(int weight)
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 40);
            core.AddOrUpdate(2, weight);
            core.DoMaintenance();

            core.TryGet(1, out _).Should().BeTrue();
            core.TryGet(2, out _).Should().BeFalse();
            core.policy.eviction.WeightedSize.Should().Be(40);
            AssertWeightedState(core);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        public void DoMaintenanceAdmitsOnlyMoreFrequentWeightedCandidate(int readKey, int retainedKey)
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 80);
            core.DoMaintenance();
            core.AddOrUpdate(2, 80);
            for (int read = 0; read < 8; read++)
            {
                core.TryGet(readKey, out _);
            }

            core.DoMaintenance();

            core.Keys.Should().ContainSingle().Which.Should().Be(retainedKey);
            AssertWeightedState(core);
        }

        [Fact]
        public void TryUpdateOversizedWeightEvictsOnlyUpdatedEntry()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 10);
            core.AddOrUpdate(2, 40);
            core.DoMaintenance();
            core.TryGet(1, out _);
            core.DoMaintenance();

            core.TryUpdate(1, int.MaxValue).Should().BeTrue();
            core.DoMaintenance();

            core.Keys.Should().ContainSingle().Which.Should().Be(2);
            core.policy.eviction.WeightedSize.Should().Be(40);
            AssertWeightedState(core);
        }

        [Theory]
        [InlineData(0, 50)]
        [InlineData(50, 0)]
        [InlineData(30, 70)]
        [InlineData(70, 30)]
        public void TryUpdateChangedWeightReconcilesAccounting(int initialWeight, int updatedWeight)
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, initialWeight);
            core.DoMaintenance();

            core.TryUpdate(1, updatedWeight).Should().BeTrue();
            core.DoMaintenance();

            core.policy.eviction.WeightedSize.Should().Be(updatedWeight);
            AssertWeightedState(core);
        }

        [Fact]
        public void TryUpdateWeightChangedWithFullWriteBufferDoesNotLoseAccounting()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 30);
            core.AddOrUpdate(2, 1);
            core.DoMaintenance();
            for (int i = 0; i < core.writeBuffer.Capacity; i++)
            {
                core.TryUpdate(2, 1);
            }
            core.writeBuffer.Count.Should().Be(core.writeBuffer.Capacity);

            core.TryUpdate(1, 50).Should().BeTrue();
            core.DoMaintenance();

            core.policy.eviction.WeightedSize.Should().Be(51);
            AssertWeightedState(core);
        }

        [Fact]
        public void TryGetProbationEntryPromotesItsAccountedWeight()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 10);
            core.AddOrUpdate(2, 20);
            core.DoMaintenance();

            core.TryGet(1, out _).Should().BeTrue();
            core.DoMaintenance();

            core.policy.eviction.ProtectedWeightedSize.Should().Be(10);
            AssertWeightedState(core);
        }

        [Fact]
        public void TryGetEntryTooHeavyForProtectedKeepsItInProbation()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 80);
            core.DoMaintenance();

            core.TryGet(1, out _).Should().BeTrue();
            core.DoMaintenance();

            core.policy.eviction.Probation.Select(n => n.Key).Should().ContainSingle().Which.Should().Be(1);
            core.policy.eviction.ProtectedWeightedSize.Should().Be(0);
            AssertWeightedState(core);
        }

        [Fact]
        public void TryGetProtectedOverflowDemotesLeastRecentlyUsedWeight()
        {
            var core = CreateWeighted();
            for (int key = 1; key <= 3; key++)
            {
                core.AddOrUpdate(key, 30);
            }
            core.DoMaintenance();
            for (int key = 1; key <= 3; key++)
            {
                core.TryGet(key, out _);
            }
            core.DoMaintenance();

            core.policy.eviction.Protected.Select(n => n.Key).Should().Equal(2, 3);
            core.policy.eviction.ProtectedWeightedSize.Should().Be(60);
            AssertWeightedState(core);
        }

        [Fact]
        public void TryUpdateProtectedEntryBeyondProtectedBudgetDemotesIt()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 10);
            core.DoMaintenance();
            core.TryGet(1, out _);
            core.DoMaintenance();

            core.TryUpdate(1, 90);
            core.DoMaintenance();

            core.policy.eviction.ProtectedWeightedSize.Should().Be(0);
            core.policy.eviction.Probation.Select(n => n.Key).Should().ContainSingle().Which.Should().Be(1);
            AssertWeightedState(core);
        }

        [Fact]
        public void TryUpdateProtectedEntryAtCapacityEvictsWindowWhenProbationIsEmpty()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 10);
            core.DoMaintenance();
            core.TryGet(1, out _);
            core.DoMaintenance();
            core.AddOrUpdate(2, 1);
            core.DoMaintenance();
            core.policy.eviction.Probation.Should().BeEmpty();
            core.policy.eviction.Protected.Select(n => n.Key).Should().ContainSingle().Which.Should().Be(1);

            core.TryUpdate(1, core.Capacity).Should().BeTrue();
            core.DoMaintenance();

            core.Keys.Should().ContainSingle().Which.Should().Be(1);
            core.policy.eviction.WindowWeightedSize.Should().Be(0);
            core.policy.eviction.WeightedSize.Should().Be(core.Capacity);
            AssertWeightedState(core);
        }

        [Fact]
        public void TryRemoveDuplicatedBufferedWritesDiscountsOldNodeOnlyOnce()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 10);
            core.DoMaintenance();
            core.TryGetNode(1, out var removedNode);
            core.TryUpdate(1, 20);
            core.TryUpdate(1, 30);
            core.TryRemove(1);
            core.AddOrUpdate(1, 40);
            core.DoMaintenance();

            removedNode.PolicyWeight.Should().Be(0);
            core.policy.eviction.WeightedSize.Should().Be(40);
            core.Metrics.Value.Evicted.Should().Be(1);
            AssertWeightedState(core);
        }

        [Fact]
        public void TryRemoveBeforeInsertionDrainsDoesNotSubtractUnaccountedWeight()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 10);
            core.TryRemove(1);
            core.DoMaintenance();

            core.policy.eviction.WeightedSize.Should().Be(0);
            AssertWeightedState(core);
        }

        [Fact]
        public void TrimThroughBoxedPolicySharesAccountingWithOriginalCore()
        {
            var core = CreateWeighted();
            for (int key = 0; key < 5; key++)
            {
                core.AddOrUpdate(key, 10);
            }
            core.DoMaintenance();

            core.Policy.Eviction.Value.Trim(2);

            core.Count.Should().Be(3);
            core.policy.eviction.WeightedSize.Should().Be(30);
            AssertWeightedState(core);
        }

        [Fact]
        public void ClearWithZeroWeightAndPendingUpdatesResetsAllAccounting()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 10);
            core.AddOrUpdate(2, 0);
            core.DoMaintenance();
            core.TryUpdate(1, 20);

            core.Clear();

            core.Count.Should().Be(0);
            core.policy.eviction.WeightedSize.Should().Be(0);
            AssertWeightedState(core);
        }

        [Fact]
        public void AddOrUpdateNegativeWeightThrowsWithoutInsertingEntry()
        {
            var core = CreateWeighted();
            Action add = () => core.AddOrUpdate(1, -1);

            add.Should().Throw<ArgumentOutOfRangeException>();
            core.Count.Should().Be(0);
        }

        [Fact]
        public void TryUpdateNegativeWeightThrowsWithoutChangingValueOrWeight()
        {
            var core = CreateWeighted();
            core.AddOrUpdate(1, 20);
            core.DoMaintenance();
            Action update = () => core.TryUpdate(1, -1);

            update.Should().Throw<ArgumentOutOfRangeException>();
            core.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(20);
            AssertWeightedState(core);
        }

        [Fact]
        public void DoMaintenanceChangingHitRateTransfersWholeNodesBetweenPartitions()
        {
            var core = CreateWeighted(200);
            for (int key = 0; key < 10; key++)
            {
                core.AddOrUpdate(key, 10);
            }
            core.DoMaintenance();
            var partition = core.policy.eviction.Capacity;
            int initialWindow = partition.Window;

            for (int round = 0; round < 40; round++)
            {
                for (int request = 0; request < 100; request++)
                {
                    core.TryGet(1, out _);
                }
                core.DoMaintenance();
            }

            int grownWindow = partition.Window;
            grownWindow.Should().BeGreaterThan(initialWindow);
            AssertWeightedState(core);

            for (int round = 0; round < 40; round++)
            {
                for (int request = 0; request < 100; request++)
                {
                    core.TryGet(-1, out _);
                }
                core.DoMaintenance();
            }

            partition.Window.Should().BeLessThan(grownWindow);
            (partition.Window + partition.Protected + partition.Probation).Should().Be(core.Capacity);
            AssertWeightedState(core);
        }

        [Fact]
        public void DoMaintenanceIncreasingWindowAtMaximumPreservesEntriesAndCapacity()
        {
            var core = CreateWeighted(3);
            for (int key = 0; key < 3; key++)
            {
                core.AddOrUpdate(key, 1);
            }
            core.DoMaintenance();

            for (int request = 0; request < 100; request++)
            {
                core.TryGet(2, out _);
                core.DoMaintenance();
            }

            core.policy.eviction.Capacity.Window.Should().Be(2);
            core.policy.eviction.Capacity.Protected.Should().Be(0);
            core.Keys.Should().BeEquivalentTo(new[] { 0, 1, 2 });
            AssertWeightedState(core);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RemoveWeightedExpiringEntryDeschedulesAndDiscountsItOnce(bool expireOnRead)
        {
            var core = CreateWeightedWithExpiry();
            core.AddOrUpdate(1, 20);
            core.DoMaintenance();
            core.TryGetNode(1, out var node);
            node.GetNextInTimeOrder().Should().NotBeNull();
            core.TryUpdate(1, 30);

            if (expireOnRead)
            {
                node.TimeToExpire = Duration.Zero;
                core.TryGet(1, out _).Should().BeFalse();
            }
            else
            {
                core.TryRemove(1).Should().BeTrue();
            }
            core.DoMaintenance();
            core.DoMaintenance();

            node.GetNextInTimeOrder().Should().BeNull();
            core.Metrics.Value.Evicted.Should().Be(1);
            core.policy.eviction.WeightedSize.Should().Be(0);
            AssertWeightedState(core);
        }

        [Fact]
        public void AddOrUpdateOversizedExpiringEntryDoesNotScheduleRemovedNode()
        {
            var core = CreateWeightedWithExpiry();
            core.AddOrUpdate(1, 200);
            core.TryGetNode(1, out var node);
            core.DoMaintenance();

            node.GetNextInTimeOrder().Should().BeNull();
            node.WasDeleted.Should().BeTrue();
            AssertWeightedState(core);
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void TryUpdateThrowingEventHandlerStillPublishesWeightChange()
        {
            var cache = (WeightedConcurrentLfu<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, EventPolicy<int, int>>>)
                new ConcurrentLfuBuilder<int, int>()
                    .WithCapacity(100)
                    .WithScheduler(new NullScheduler())
                    .WithWeightedEntries(new TestWeightCalculator<int, int>((k, v) => v))
                    .WithEvents()
                    .Build();
            cache.AddOrUpdate(1, 20);
            cache.DoMaintenance();
            cache.Events.Value.ItemUpdated += (sender, args) => throw new InvalidOperationException("Callback failed.");

            Action update = () => cache.TryUpdate(1, 50);
            update.Should().Throw<InvalidOperationException>();
            cache.DoMaintenance();

            cache.Core.policy.eviction.WeightedSize.Should().Be(50);
            AssertWeightedState(cache.Core);
        }
#endif

        private static ConcurrentLfuCore<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, NoEventPolicy<int, int>>, NoEventPolicy<int, int>> CreateWeighted(int capacity = 100)
        {
            return new(1, capacity, new NullScheduler(), EqualityComparer<int>.Default, () => { },
                new WeightedAccessOrderPolicy<int, int, NoEventPolicy<int, int>>(new TestWeightCalculator<int, int>((k, v) => v)), default);
        }

        private static ConcurrentLfuCore<int, int, WeightedTimeOrderNode<int, int>, WeightedExpireAfterPolicy<int, int, NoEventPolicy<int, int>>, NoEventPolicy<int, int>> CreateWeightedWithExpiry()
        {
            return new(1, 100, new NullScheduler(), EqualityComparer<int>.Default, () => { },
                new WeightedExpireAfterPolicy<int, int, NoEventPolicy<int, int>>(
                    new TestWeightCalculator<int, int>((k, v) => v), new ExpireAfterWrite<int, int>(TimeSpan.FromMinutes(10))), default);
        }

        internal static void AssertWeightedState<N, P, E>(ConcurrentLfuCore<int, int, N, P, E> core)
            where N : LfuNode<int, int>
            where P : struct, INodePolicy<int, int, N, E>, INodeWeight<N>
            where E : struct, IEventPolicy<int, int>
        {
            var eviction = (WeightedEvictionPolicy<int, int, N, P>)core.policy.Eviction;
            var nodes = eviction.Window.Concat(eviction.Probation).Concat(eviction.Protected).Cast<N>().ToArray();

            eviction.WeightedSize.Should().Be(nodes.Sum(n => (long)core.policy.GetWeight(n)));
            eviction.WindowWeightedSize.Should().Be(eviction.Window.Sum(n => (long)core.policy.GetPolicyWeight((N)n)));
            eviction.ProtectedWeightedSize.Should().Be(eviction.Protected.Sum(n => (long)core.policy.GetPolicyWeight((N)n)));
            eviction.WeightedSize.Should().BeLessThanOrEqualTo(core.Capacity);
            nodes.Length.Should().Be(core.Count);
            foreach (var node in nodes)
            {
                core.policy.GetPolicyWeight(node).Should().Be(core.policy.GetWeight(node));
            }
        }
    }

    internal sealed class TestWeightCalculator<K, V> : IWeightCalculator<K, V>
    {
        private readonly Func<K, V, int> calculate;

        internal TestWeightCalculator(Func<K, V, int> calculate)
        {
            this.calculate = calculate;
        }

        public int GetWeight(K key, V value) => this.calculate(key, value);
    }
}
