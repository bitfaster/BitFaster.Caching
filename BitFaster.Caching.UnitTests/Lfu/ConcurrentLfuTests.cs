using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using BitFaster.Caching.UnitTests.Lru;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class ConcurrentLfuTests
    {
        private readonly ITestOutputHelper output;

       private ConcurrentLfu<int, int> cache = new ConcurrentLfu<int, int>(20, new BackgroundThreadScheduler());
//         private ConcurrentLfu<int, int> cache = new ConcurrentLfu<int, int>(20, new ThreadPoolScheduler());
        // private ConcurrentLfu<int, int> cache = new ConcurrentLfu<int, int>(20, new ThreadPoolSchedulerContinuations());
    //    private ConcurrentLfu<int, int> cache = new ConcurrentLfu<int, int>(20, new ForegroundScheduler());

        public ConcurrentLfuTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void DefaultSchedulerIsBackground()
        {
            var cache = new ConcurrentLfu<int, int>(20);
            cache.Scheduler.Should().BeOfType<ThreadPoolScheduler>();
        }

        [Fact]
        public void Scenario()
        {
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(2, k => k);
            cache.GetOrAdd(2, k => k);

            for (int i = 0; i < 25; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            cache.PendingMaintenance();

            cache.TryGet(1, out var value1).Should().BeTrue();
            cache.TryGet(2, out var value2).Should().BeTrue();
            cache.Count.Should().Be(20);
        }

        [Fact]
        public void Probation()
        {
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(2, k => k);
            cache.GetOrAdd(2, k => k);

            for (int i = 0; i < 25; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            cache.PendingMaintenance();

            cache.GetOrAdd(16, k => k);

            for (int i = 25; i < 50; i++)
            {
                cache.GetOrAdd(i, k => k);
                cache.GetOrAdd(i, k => k);
            }

            cache.PendingMaintenance();

            // TODO: it is promoted, but the verification here is not correct (it is present even when not promoted)
            cache.TryGet(16, out var value1).Should().BeTrue();
        }

        [Fact]
        public void EvictionPolicyReturnsCapacity()
        {
            cache.Policy.Eviction.Value.Capacity.Should().Be(20);
        }

        [Fact]
        public void ExpireAfterWriteIsDisabled()
        {
            cache.Policy.ExpireAfterWrite.HasValue.Should().BeFalse();
        }

        [Fact]
        public void EventsAreDisabled()
        {
            cache.Events.HasValue.Should().BeFalse();
        }

        [Fact]
        public void MetricsAreEnabled()
        {
            cache.Metrics.HasValue.Should().BeTrue();
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedMetricHitRatioIsHalf()
        {
            cache.GetOrAdd(1, k => k);
            bool result = cache.TryGet(1, out var value);

            cache.PendingMaintenance();

            cache.Metrics.Value.HitRatio.Should().Be(0.5);
            cache.Metrics.Value.Hits.Should().Be(1);
            cache.Metrics.Value.Misses.Should().Be(1);
        }

        [Fact]
        public void WhenItemIsEvictedMetricRecordsCount()
        {
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(2, k => k);
            cache.GetOrAdd(2, k => k);

            for (int i = 0; i < 25; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            cache.PendingMaintenance();

            cache.Metrics.Value.Evicted.Should().Be(5);
        }

        [Fact]
        public void WhenItemsAddedKeysContainsTheKeys()
        {
            cache.Count.Should().Be(0);
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(2, k => k);
            cache.Keys.Should().BeEquivalentTo(new[] { 1, 2 });
        }

        [Fact]
        public void WhenItemsAddedGenericEnumerateContainsKvps()
        {
            cache.Count.Should().Be(0);
            cache.GetOrAdd(1, k => k + 1);
            cache.GetOrAdd(2, k => k + 1);

            cache.Should().BeEquivalentTo(new[] { new KeyValuePair<int, int>(1, 2), new KeyValuePair<int, int>(2, 3) });
        }

        [Fact]
        public void WhenItemsAddedEnumerateContainsKvps()
        {
            cache.Count.Should().Be(0);
            cache.GetOrAdd(1, k => k + 1);
            cache.GetOrAdd(2, k => k + 1);

            var enumerable = (IEnumerable)cache;
            enumerable.Should().BeEquivalentTo(new[] { new KeyValuePair<int, int>(1, 2), new KeyValuePair<int, int>(2, 3) });
        }

        [Fact]
        public void WhenItemIsUpdatedItIsUpdated()
        {
            cache.GetOrAdd(1, k => k);
            cache.AddOrUpdate(1, 2);

            cache.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(2);
        }

        [Fact]
        public void WhenItemDoesNotExistUpdatedAddsItem()
        {
            cache.AddOrUpdate(1, 2);

            cache.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(2);
        }

        [Fact]
        public void WhenItemIsRemovedItIsRemoved()
        {
            cache.GetOrAdd(1, k => k);

            cache.TryRemove(1).Should().BeTrue();
            cache.TryGet(1, out var value).Should().BeFalse();
        }

        [Fact]
        public void WhenItemDoesNotExistTryRemoveIsFalse()
        {
            cache.TryRemove(1).Should().BeFalse();
        }

        // OnWrite handles the case where a node is removed while the write buffer contains the node
        [Fact]
        public void WhenRemovedInWriteBuffer()
        {
            cache.GetOrAdd(1, k => k);

            // wait for the maintenance thread to run, this will attach he new node to the LRU list
            cache.PendingMaintenance();

            // pending write in the buffer
            cache.TryUpdate(1, 2);

            // immediately remove
            cache.TryRemove(1).Should().BeTrue();

            // TODO: how to verify maintenance completed ok?
            cache.PendingMaintenance();

            cache.TryGet(1, out var _).Should().BeFalse();
        }

        [Fact]
        public void WhenItemDoesNotExistTryUpdateIsFalse()
        {
            cache.TryUpdate(1, 2).Should().BeFalse();
        }

        [Fact]
        public void WhenClearedCacheIsEmpty()
        {
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(2, k => k);
            cache.PendingMaintenance();

            cache.Clear();
            cache.PendingMaintenance();

            cache.Count.Should().Be(0);
            cache.TryGet(1, out var _).Should().BeFalse();
        }

        [Fact]
        public void TrimRemovesNItems()
        {
            for (int i = 0; i < 25; i++)
            {
                cache.GetOrAdd(i, k => k);
            }
            cache.PendingMaintenance();

            cache.Count.Should().Be(20);

            cache.Trim(5);
            cache.PendingMaintenance();

            cache.Count.Should().Be(15);
        }

        [Fact]
        public void TrimWhileItemsInWriteBufferRemovesNItems()
        {
            for (int i = 0; i < 25; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            cache.Trim(5);

            cache.PendingMaintenance();

            // TODO: How does this happen?
            // The trim takes effect before all the writes are replayed by the maintenance thread.
            cache.Metrics.Value.Evicted.Should().Be(5);
            cache.Count.Should().BeLessThanOrEqualTo(20);

            this.output.WriteLine($"Count {cache.Count}");
            this.output.WriteLine($"Keys {string.Join(",", cache.Keys.Select(k => k.ToString()))}");
        }

        // ~453 ms (Release)
        // Cache hits 1,943,550 (20%)
        // Maintenance ops 27
        [Fact]
        public void BenchBackground()
        {
            DebugBench();
        }

        // 494 ms (Release)
        // Cache hits 3,462,597 (35%)
        // Maintenance ops 15
        [Fact]
        public void BenchThreadPool()
        {
            cache = new ConcurrentLfu<int, int>(20, new ThreadPoolScheduler());
            DebugBench();
        }

        // 766 ms (Release)
        // Cache hits 9,922,432 (99%)
        // Maintenance ops 77,520
        [Fact]
        public void BenchForeground()
        {
            cache = new ConcurrentLfu<int, int>(20, new ForegroundScheduler());
            DebugBench();
        }


        private void DebugBench()
        {
            Func<int, int> func = x => x;

            for (int i = 0; i < 10000000; i++)
            {
                cache.GetOrAdd(1, func);
            }

            this.output.WriteLine($"Cache hits {this.cache.Metrics.Value.Hits}");
            this.output.WriteLine($"Maintenance ops {this.cache.Scheduler.RunCount}");

            if (this.cache.Scheduler.LastException.HasValue)
            {
                this.output.WriteLine($"Error: {this.cache.Scheduler.LastException.Value}");
            }

            // verify this doesn't block or throw
            var b = cache.Scheduler as BackgroundThreadScheduler;
            if (b is not null)
            {
                b.Dispose();
            }
        }
    }
}
