using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class ConcurrentLfuTests
    {
        private readonly ITestOutputHelper output;

        private ConcurrentLfu<int, int> cache = new ConcurrentLfu<int, int>(1, 20, new BackgroundThreadScheduler());
        private ValueFactory valueFactory = new ValueFactory();

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
        public void WhenKeyIsRequestedItIsCreatedAndCached()
        {
            var result1 = cache.GetOrAdd(1, valueFactory.Create);
            var result2 = cache.GetOrAdd(1, valueFactory.Create);

            valueFactory.timesCalled.Should().Be(1);
            result1.Should().Be(result2);
        }

        [Fact]
        public async Task WhenKeyIsRequesteItIsCreatedAndCachedAsync()
        {
            var result1 = await cache.GetOrAddAsync(1, valueFactory.CreateAsync).ConfigureAwait(false);
            var result2 = await cache.GetOrAddAsync(1, valueFactory.CreateAsync).ConfigureAwait(false);

            valueFactory.timesCalled.Should().Be(1);
            result1.Should().Be(result2);
        }

        [Fact]
        public void WhenItemsAddedExceedsCapacityItemsAreDiscarded()
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

        // protected 15
        // probation 4
        // window 1
        [Fact]
        public void WhenNewItemsAreAddedTheyArePromotedBasedOnFrequency()
        {
            for (int i = 0; i < 20; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            // W [19] Protected [] Probation [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            for (int i = 0; i < 15; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            // W [19] Protected [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14] Probation [15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            for (int k = 0; k < 2; k++)
            {
                for (int j = 0; j < 6; j++)
                {
                    for (int i = 0; i < 15; i++)
                    {
                        cache.GetOrAdd(j + 20, k => k);
                    }
                    cache.PendingMaintenance();
                    LogLru();
                }
            }

            // Values promoted to probation then protected:
            // W[21] Protected[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14] Probation[16, 17, 18, 20]
            // W[22] Protected[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14] Probation[17, 18, 20, 21]
            // W[23] Protected[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14] Probation[18, 20, 21, 22]
            // W[24] Protected[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14] Probation[20, 21, 22, 23]
            // W[25] Protected[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14] Probation[20, 21, 22, 23]
            // W[25] Protected[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 20] Probation[21, 22, 23, 0]
            // W[25] Protected[2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 20, 21] Probation[22, 23, 0, 1]
            // W[25] Protected[3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 20, 21, 22] Probation[23, 0, 1, 2]
            // W[25] Protected[4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 20, 21, 22, 23] Probation[0, 1, 2, 3]
            // W[24] Protected[4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 20, 21, 22, 23] Probation[1, 2, 3, 25]
            // W[24] Protected[5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 20, 21, 22, 23, 25] Probation[1, 2, 3, 4]

            cache.Count.Should().Be(20);

            // W [24] Protected [5,6,7,8,9,10,11,12,13,14,20,21,22,23,25] Probation []
            cache.Trim(4);
            cache.PendingMaintenance();
            LogLru();

            cache.TryGet(1, out var value1).Should().BeFalse();
            cache.TryGet(2, out var value2).Should().BeFalse();
            cache.Count.Should().Be(16);
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

            // W [24] Protected [] Probation [1,2,0,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            cache.GetOrAdd(16, k => k);

            for (int i = 25; i < 50; i++)
            {
                cache.GetOrAdd(i, k => k);
                cache.GetOrAdd(i, k => k);
            }

            // W [49] Protected [16] Probation [1,2,0,3,4,5,6,7,8,9,10,11,12,13,14,15,17,18]
            cache.PendingMaintenance();
            LogLru();

            cache.Trim(18);

            // W [49] Protected [16] Probation []
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

            //  W [24] Protected [] Probation [1,2,0,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            cache.TryUpdate(16, -16).Should().BeTrue();

            for (int i = 25; i < 50; i++)
            {
                cache.GetOrAdd(i, k => k);
                cache.GetOrAdd(i, k => k);
            }

            // [49] Protected [16] Probation [1,2,0,3,4,5,6,7,8,9,10,11,12,13,14,15,17,18]
            cache.PendingMaintenance();
            LogLru();

            cache.Trim(18);

            // W [49] Protected [16] Probation []
            cache.PendingMaintenance();
            LogLru();

            cache.TryGet(16, out var value1).Should().BeTrue();
        }

        [Fact]
        public void ReadUpdatesProtectedLruOrder()
        {
            // W [19] Protected [] Probation [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18]
            for (int i = 0; i < 20; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            cache.PendingMaintenance();
            LogLru();

            cache.GetOrAdd(7, k => k);
            cache.GetOrAdd(8, k => k);
            cache.GetOrAdd(9, k => k);

            // W [19] Protected [7,8,9] Probation [0,1,2,3,4,5,6,10,11,12,13,14,15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            // W [19] Protected [8,9,7] Probation [0,1,2,3,4,5,6,10,11,12,13,14,15,16,17,18]
            // element 7 now moved to back of LRU
            cache.GetOrAdd(7, k => k);
            cache.PendingMaintenance();
            LogLru();

            // Trim is LRU order
            //W [19] Protected [7] Probation []
            cache.Trim(18);
            cache.PendingMaintenance();
            LogLru();

            cache.TryGet(7, out var _).Should().BeTrue();
        }

        [Fact]
        public void WriteUpdatesProtectedLruOrder()
        {
            // W [19] Protected [] Probation [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18]
            for (int i = 0; i < 20; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            cache.PendingMaintenance();
            LogLru();

            cache.GetOrAdd(7, k => k);
            cache.GetOrAdd(8, k => k);
            cache.GetOrAdd(9, k => k);

            // W [19] Protected [7,8,9] Probation [0,1,2,3,4,5,6,10,11,12,13,14,15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            // W [19] Protected [8,9,7] Probation [0,1,2,3,4,5,6,10,11,12,13,14,15,16,17,18]
            // element 7 now moved to back of LRU
            cache.TryUpdate(7, -7).Should().BeTrue();
            cache.PendingMaintenance();
            LogLru();

            // Trim is LRU order
            //W [19] Protected [7] Probation []
            cache.Trim(18);
            cache.PendingMaintenance();
            LogLru();

            cache.TryGet(7, out var _).Should().BeTrue();
        }

        [Fact]
        public void WhenHitRateChangesWindowSizeIsAdapted()
        {
            cache = new ConcurrentLfu<int, int>(1, 20, new NullScheduler());

            // First completely fill the cache, push entries into protected
            for (int i = 0; i < 20; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            // W [19] Protected [] Probation [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            for (int i = 0; i < 15; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            // W [19] Protected [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14] Probation [15,16,17,18]
            cache.PendingMaintenance();
            LogLru();

            // The reset sample size is 200, so do 200 cache hits
            // W [19] Protected [12,13,14,15,16,17,18,0,1,2,3,4,5,6,7] Probation [8,9,10,11]
            for (int j = 0; j < 10; j++)
                for (int i = 0; i < 20; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            cache.PendingMaintenance();
            LogLru();

            // then miss 200 times
            // W [300] Protected [12,13,14,15,16,17,18,0,1,2,3,4,5,6,7] Probation [9,10,11,227]
            for (int i = 0; i < 201; i++)
            {
                cache.GetOrAdd(i + 100, k => k);
            }

            cache.PendingMaintenance();
            LogLru();

            // then miss 200 more times (window adaptation +1 window slots)
            // W [399,400] Protected [14,15,16,17,18,0,1,2,3,4,5,6,7,227] Probation [9,10,11,12]
            for (int i = 0; i < 201; i++)
            {
                cache.GetOrAdd(i + 200, k => k);
            }

            cache.PendingMaintenance();
            LogLru();

            // make 2 requests to new keys, if window is size is now 2 both will exist:
            cache.GetOrAdd(666, k => k);
            cache.GetOrAdd(667, k => k);

            cache.PendingMaintenance();
            LogLru();

            cache.TryGet(666, out var _).Should().BeTrue();
            cache.TryGet(667, out var _).Should().BeTrue();

            this.output.WriteLine($"Scheduler ran {cache.Scheduler.RunCount} times.");
        }

        [Fact]
        public void ReadSchedulesMaintenanceWhenBufferIsFull()
        {
            var scheduler = new TestScheduler();
            cache = new ConcurrentLfu<int, int>(1, 20, scheduler);

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
            cache = new ConcurrentLfu<int, int>(1, 20, scheduler);

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
            cache = new ConcurrentLfu<int, int>(1, ConcurrentLfu<int, int>.BufferSize * 2, scheduler);

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
            cache = new ConcurrentLfu<int, int>(1, 20, scheduler);

            cache.GetOrAdd(1, k => k);
            scheduler.RunCount.Should().Be(1);
            cache.PendingMaintenance();

            for (int i = 0; i < bufferSize * 2; i++)
            {
                cache.TryUpdate(1, i);
            }

            cache.PendingMaintenance();

            cache.Metrics.Value.Updated.Should().Be(bufferSize);
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
        public void WhenItemIsRemovedEvictionCountIsIncremented()
        {
            cache.GetOrAdd(1, k => k);

            cache.TryRemove(1).Should().BeTrue();
            cache.PendingMaintenance();

            // TODO: currently we count twice
            cache.Metrics.Value.Evicted.Should().BeGreaterThan(1);
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
            // null scheduler == no maintenance, all writes fit in buffer
            cache = new ConcurrentLfu<int, int>(1, 20, new NullScheduler());

            for (int i = 0; i < 25; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            // Trim implicitly performs maintenance
            cache.Trim(5);

            cache.PendingMaintenance();

            // The trim takes effect before all the writes are replayed by the maintenance thread.
            cache.Metrics.Value.Evicted.Should().Be(10);
            cache.Count.Should().Be(15);

            this.output.WriteLine($"Count {cache.Count}");
            this.output.WriteLine($"Keys {string.Join(",", cache.Keys.Select(k => k.ToString()))}");
        }

        //Elapsed 411.6918ms - 0.0004116918ns/op
        //Cache hits 1689839 (sampled 16.89839%)
        //Maintenance ops 31
        [Fact]
        public void VerifyHitsWithBackgroundScheduler()
        {
            // when running all tests in parallel, sample count drops significantly: set low bar for stability.
            VerifyHits(iterations: 10000000, minSamples: 250000);
        }

        //Elapsed 590.8154ms - 0.0005908154ns/op
        //Cache hits 3441470 (sampled 34.414699999999996%)
        //Maintenance ops 20
        [Fact]
        public void VerifyHitsWithThreadPoolScheduler()
        {
            // when running all tests in parallel, sample count drops significantly: set low bar for stability.
            cache = new ConcurrentLfu<int, int>(1, 20, new ThreadPoolScheduler());
            VerifyHits(iterations: 10000000, minSamples: 500000);
        }

        //Elapsed 273.0148ms - 0.0002730148ns/op
        //Cache hits 0 (sampled 0%)
        //Maintenance ops 1
        [Fact]
        public void VerifyHitsWithNullScheduler()
        {
            cache = new ConcurrentLfu<int, int>(1, 20, new NullScheduler());
            VerifyHits(iterations: 10000000, minSamples: -1);
        }

        //Will drop 78125 reads.
        //Elapsed 847.5331ms - 0.0008475331ns/op
        //Cache hits 10000000 (sampled 99.2248062015504%)
        //Maintenance ops 78126
        [Fact]
        public void VerifyHitsWithForegroundScheduler()
        {
            cache = new ConcurrentLfu<int, int>(1, 20, new ForegroundScheduler());

            // Note: TryAdd will drop 1 read per full read buffer, since TryAdd will return false
            // before TryScheduleDrain is called. This serves as sanity check.
            int iterations = 10000000;
            int dropped = iterations / ConcurrentLfu<int, int>.BufferSize;

            this.output.WriteLine($"Will drop {dropped} reads.");

            VerifyHits(iterations: iterations + dropped, minSamples: iterations);
        }

        private void VerifyHits(int iterations, int minSamples)
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
            var samplePercent = this.cache.Metrics.Value.Hits / (double)iterations * 100;

            this.output.WriteLine($"Elapsed {timeMs}ms - {timeNs}ns/op");
            this.output.WriteLine($"Cache hits {this.cache.Metrics.Value.Hits} (sampled {samplePercent}%)");
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

        public class ValueFactory
        {
            public int timesCalled;

            public int Create(int key)
            {
                timesCalled++;
                return key;
            }

            public Task<int> CreateAsync(int key)
            {
                timesCalled++;
                return Task.FromResult(key);
            }
        }
    }
}
