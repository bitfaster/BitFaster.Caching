using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

        public ConcurrentLfuTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void DefaultSchedulerIsThreadPool()
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
        public void ReadPromotesProbation()
        {
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(2, k => k);
            cache.GetOrAdd(2, k => k);

            for (int i = 0; i < 25; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            // W [24] Protected [1,2,0,3,4,5,6,7,8,9,10,11,12,13,14] Probation [15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            cache.GetOrAdd(16, k => k);

            for (int i = 25; i < 50; i++)
            {
                cache.GetOrAdd(i, k => k);
                cache.GetOrAdd(i, k => k);
            }

            // W [49] Protected [2,0,3,4,5,6,7,8,9,10,11,12,13,14,16] Probation [1,25,26,27]
            cache.PendingMaintenance();
            LogLru();

            cache.Trim(5);

            // W [49] Protected [0,3,4,5,6,7,8,9,10,11,12,13,14,16] Probation []
            cache.PendingMaintenance();
            LogLru();

            cache.TryGet(16, out var value1).Should().BeTrue();
        }

        // when probation item is written it is moved to protected
        [Fact]
        public void WritePromotesProbation()
        {
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(2, k => k);
            cache.GetOrAdd(2, k => k);

            for (int i = 0; i < 25; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            // W [24] Protected [1,2,0,3,4,5,6,7,8,9,10,11,12,13,14] Probation [15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            cache.TryUpdate(16, -16).Should().BeTrue();

            for (int i = 25; i < 50; i++)
            {
                cache.GetOrAdd(i, k => k);
                cache.GetOrAdd(i, k => k);
            }

            // W [49] Protected [2,0,3,4,5,6,7,8,9,10,11,12,13,14,16] Probation [1,25,26,27]
            cache.PendingMaintenance();
            LogLru();

            cache.Trim(5);

            // W [49] Protected [0,3,4,5,6,7,8,9,10,11,12,13,14,16] Probation []
            cache.PendingMaintenance();
            LogLru();

            cache.TryGet(16, out var value1).Should().BeTrue();
        }

        [Fact]
        public void WriteUpdateProtectedLruOrder()
        {
            // W [19], Protected [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14], Probation [15, 16, 17, 18]
            for (int i = 0; i < 20; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            cache.PendingMaintenance();

            // W [19], Protected [0, 1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 7], Probation [15, 16, 17, 18]
            // element 7 now moved to back of LRU
            cache.TryUpdate(7, -7).Should().BeTrue();
            cache.PendingMaintenance();

            // Trim is LRU order
            // W [19], Protected [9, 10, 11, 12, 14, 7], Probation []
            cache.Trim(12);
            cache.PendingMaintenance();

            cache.TryGet(7, out var _).Should().BeTrue();
        }

        [Fact]
        public void ReadSchedulesMaintenanceWhenBufferIsFull()
        {
            var scheduler = new TestScheduler();
            cache = new ConcurrentLfu<int, int>(20, scheduler);

            cache.GetOrAdd(1, k => k);
            scheduler.RunCount.Should().Be(1);
            cache.PendingMaintenance();

            for (int i = 0; i < ConcurrentLfu<int, int>.BufferSize; i++)
            {
                scheduler.RunCount.Should().Be(1);
                cache.GetOrAdd(1, k => k);
            }

            // read buffer is now full, next read triggers maintenance
            cache.GetOrAdd(1, k => k);
            scheduler.RunCount.Should().Be(2);
        }

        [Fact]
        public void WhenReadBufferIsFullReadsAreDropped()
        {
            int bufferSize = ConcurrentLfu<int, int>.BufferSize;
            var scheduler = new TestScheduler();
            cache = new ConcurrentLfu<int, int>(20, scheduler);

            cache.GetOrAdd(1, k => k);
            scheduler.RunCount.Should().Be(1);
            cache.PendingMaintenance();

            for (int i = 0; i < bufferSize * 2; i++)
            {
                cache.GetOrAdd(1, k => k);
            }

            cache.PendingMaintenance();

            cache.Metrics.Value.Hits.Should().Be(bufferSize);
        }

        [Fact]
        public void WhenWriteBufferIsFullAddDoesMaintenance()
        {
            var scheduler = new TestScheduler();
            cache = new ConcurrentLfu<int, int>(ConcurrentLfu<int, int>.BufferSize * 2, scheduler);

            // add an item, flush write buffer
            cache.GetOrAdd(-1, k => k);
            scheduler.RunCount.Should().Be(1);
            cache.PendingMaintenance();

            // remove the item but don't flush, it is now in the write buffer and maintenance is scheduled
            cache.TryRemove(-1).Should().BeTrue();
            scheduler.RunCount.Should().Be(2);

            // add buffer size items, last iteration will invoke maintenance on the foreground since write
            // buffer is full and test scheduler did not do any work
            for (int i = 0; i < ConcurrentLfu<int, int>.BufferSize; i++)
            {
                scheduler.RunCount.Should().Be(2);
                cache.GetOrAdd(i, k => k);
            }

            // pending write (to remove -1) should be flushed by the 128th write calling maintenance
            // directly within AfterWrite
            cache.TryGet(-1, out var _).Should().BeFalse();
        }

        [Fact]
        public void WhenWriteBufferIsFullUpdatesAreDropped()
        {
            int bufferSize = ConcurrentLfu<int, int>.BufferSize;
            var scheduler = new TestScheduler();
            cache = new ConcurrentLfu<int, int>(20, scheduler);

            cache.GetOrAdd(-1, k => k);
            scheduler.RunCount.Should().Be(1);
            cache.PendingMaintenance();

            for (int i = 0; i < bufferSize * 2; i++)
            {
                cache.TryUpdate(1, i);
            }

            cache.PendingMaintenance();
            
            // TODO: how to verify this? There is no counter for updates.
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

            // wait for the maintenance thread to run, this will attach the new node to the LRU list
            cache.PendingMaintenance();

            // pending write in the buffer
            cache.TryUpdate(1, 2);

            // immediately remove
            cache.TryRemove(1).Should().BeTrue();

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

            // The trim takes effect before all the writes are replayed by the maintenance thread.
            cache.Metrics.Value.Evicted.Should().Be(5);
            cache.Count.Should().BeLessThanOrEqualTo(20);

            this.output.WriteLine($"Count {cache.Count}");
            this.output.WriteLine($"Keys {string.Join(",", cache.Keys.Select(k => k.ToString()))}");
        }

        // ~453 ms (Release) - approx 60% slower than no background thread
        // Cache hits 1,943,550 (20%)
        // Maintenance ops 27
        [Fact]
        public void BenchBackground()
        {
            // when running all tests in parallel, sample count drops significantly: set low bar for stability.
            DebugBench(iterations: 10000000, minSamples: 500000);
        }

        // 494 ms (Release)
        // Cache hits 3,462,597 (35%)
        // Maintenance ops 15
        [Fact]
        public void BenchThreadPool()
        {
            cache = new ConcurrentLfu<int, int>(20, new ThreadPoolScheduler());
            DebugBench(iterations: 10000000, minSamples: 3000000);
        }

        // 284 ms
        [Fact]
        public void BenchNull()
        {
            cache = new ConcurrentLfu<int, int>(20, new NullScheduler());
            DebugBench(iterations: 10000000, minSamples: -1);
        }

        // 849 ms (Release)
        // Cache hits 9,922,432 (99%)
        // Maintenance ops 77,520
        [Fact]
        public void BenchForeground()
        {
            cache = new ConcurrentLfu<int, int>(20, new ForegroundScheduler());

            // Note: TryAdd will drop 1 read per full read buffer, since TryAdd will return false
            // before TryScheduleDrain is called. This serves as sanity check.
            int iterations = 10000000;
            int dropped = iterations / ConcurrentLfu<int, int>.BufferSize;

            this.output.WriteLine($"Will drop {dropped} reads.");

            DebugBench(iterations: iterations + dropped, minSamples: iterations);
        }

        private void DebugBench(int iterations, int minSamples)
        {
            Func<int, int> func = x => x;
            cache.GetOrAdd(1, func);

            var start = Stopwatch.GetTimestamp();

            for (int i = 0; i < iterations; i++)
            {
                cache.GetOrAdd(1, func);
            }

            var end = Stopwatch.GetTimestamp();

            var totalTicks = end - start;
            var timeMs = ((double)totalTicks / Stopwatch.Frequency) * 1000.0;
            var timeNs = timeMs / 1000000;

            var timePerOp = timeMs / (double)iterations;

            this.output.WriteLine($"Elapsed {timeMs}ms - {timeNs}ns/op");
            this.output.WriteLine($"Cache hits {this.cache.Metrics.Value.Hits}");
            this.output.WriteLine($"Maintenance ops {this.cache.Scheduler.RunCount}");

            if (this.cache.Scheduler.LastException.HasValue)
            {
                this.output.WriteLine($"Error: {this.cache.Scheduler.LastException.Value}");
            }

            cache.Metrics.Value.Hits.Should().BeGreaterThanOrEqualTo(minSamples);

            // verify this doesn't block or throw
            var b = cache.Scheduler as BackgroundThreadScheduler;
            if (b is not null)
            {
                b.Dispose();
            }
        }

        private void LogLru()
        {
#if DEBUG
            this.output.WriteLine(cache.FormatLruString());
#endif        
        }
    }
}
