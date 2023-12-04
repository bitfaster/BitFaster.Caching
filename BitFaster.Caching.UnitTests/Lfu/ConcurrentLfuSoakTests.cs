using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Scheduler;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lfu
{
    [Collection("Soak")]
    public class ConcurrentLfuSoakTests
    {
        private const int soakIterations = 100;
        private const int threads = 4;
        private const int loopIterations = 100_000;

        private readonly ITestOutputHelper output;
        public ConcurrentLfuSoakTests(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
        }

        //Elapsed 411.6918ms - 0.0004116918ns/op
        //Cache hits 1689839 (sampled 16.89839%)
        //Maintenance ops 31
        [Fact]
        public void VerifyHitsWithBackgroundScheduler()
        {
            var cache = new ConcurrentLfu<int, int>(1, 20, new BackgroundThreadScheduler(), EqualityComparer<int>.Default);
            // when running all tests in parallel, sample count drops significantly: set low bar for stability.
            VerifyHits(cache, iterations: 10_000_000, minSamples: 250_000);
        }

        //Elapsed 590.8154ms - 0.0005908154ns/op
        //Cache hits 3441470 (sampled 34.414699999999996%)
        //Maintenance ops 20
        [Fact]
        public void VerifyHitsWithThreadPoolScheduler()
        {
            // when running all tests in parallel, sample count drops significantly: set low bar for stability.
            var cache = new ConcurrentLfu<int, int>(1, 20, new ThreadPoolScheduler(), EqualityComparer<int>.Default);
            VerifyHits(cache, iterations: 10_000_000, minSamples: 500_000);
        }

        //Elapsed 273.0148ms - 0.0002730148ns/op
        //Cache hits 0 (sampled 0%)
        //Maintenance ops 1
        [Fact]
        public void VerifyHitsWithNullScheduler()
        {
            var cache = new ConcurrentLfu<int, int>(1, 20, new NullScheduler(), EqualityComparer<int>.Default);
            VerifyHits(cache, iterations: 10_000_000, minSamples: -1);
        }

        //Will drop 78125 reads.
        //Elapsed 847.5331ms - 0.0008475331ns/op
        //Cache hits 10000000 (sampled 99.2248062015504%)
        //Maintenance ops 78126
        [Fact]
        public void VerifyHitsWithForegroundScheduler()
        {
            var cache = new ConcurrentLfu<int, int>(1, 20, new ForegroundScheduler(), EqualityComparer<int>.Default);

            // Note: TryAdd will drop 1 read per full read buffer, since TryAdd will return false
            // before TryScheduleDrain is called. This serves as sanity check.
            int iterations = 10_000_000;
            int dropped = iterations / ConcurrentLfu<int, int>.DefaultBufferSize;

            this.output.WriteLine($"Will drop {dropped} reads.");

            VerifyHits(cache, iterations: iterations + dropped, minSamples: iterations);
        }

        [Fact]
        public void VerifyMisses()
        {
            var cache = new ConcurrentLfu<int, int>(1, 20, new BackgroundThreadScheduler(), EqualityComparer<int>.Default);

            int iterations = 100_000;
            Func<int, int> func = x => x;

            var start = Stopwatch.GetTimestamp();

            for (int i = 0; i < iterations; i++)
            {
                cache.GetOrAdd(i, func);
            }

            var end = Stopwatch.GetTimestamp();

            cache.DoMaintenance();

            var totalTicks = end - start;
            var timeMs = ((double)totalTicks / Stopwatch.Frequency) * 1000.0;
            var timeNs = timeMs / 1_000_000;

            var timePerOp = timeMs / (double)iterations;
            var samplePercent = cache.Metrics.Value.Misses / (double)iterations * 100;

            this.output.WriteLine($"Elapsed {timeMs}ms - {timeNs}ns/op");
            this.output.WriteLine($"Cache misses {cache.Metrics.Value.Misses} (sampled {samplePercent}%)");
            this.output.WriteLine($"Maintenance ops {cache.Scheduler.RunCount}");

            cache.Metrics.Value.Misses.Should().Be(iterations);
        }

        private void VerifyHits(ConcurrentLfu<int, int> cache, int iterations, int minSamples)
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
            var timeNs = timeMs / 1_000_000;

            var timePerOp = timeMs / (double)iterations;
            var samplePercent = cache.Metrics.Value.Hits / (double)iterations * 100;

            this.output.WriteLine($"Elapsed {timeMs}ms - {timeNs}ns/op");
            this.output.WriteLine($"Cache hits {cache.Metrics.Value.Hits} (sampled {samplePercent}%)");
            this.output.WriteLine($"Maintenance ops {cache.Scheduler.RunCount}");

            if (cache.Scheduler.LastException.HasValue)
            {
                this.output.WriteLine($"Error: {cache.Scheduler.LastException.Value}");
            }

            cache.Metrics.Value.Hits.Should().BeGreaterThanOrEqualTo(minSamples);

            // verify this doesn't block or throw
            var b = cache.Scheduler as BackgroundThreadScheduler;
            b?.Dispose();
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentGetCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateWithBackgroundScheduler();

            await Threaded.Run(threads, () => {
                for (int i = 0; i < loopIterations; i++)
                {
                    lfu.GetOrAdd(i + 1, i => i.ToString());
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentGetAsyncCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateWithBackgroundScheduler();

            await Threaded.RunAsync(threads, async () => {
                for (int i = 0; i < loopIterations; i++)
                {
                    await lfu.GetOrAddAsync(i + 1, i => Task.FromResult(i.ToString()));
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentGetWithArgCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateWithBackgroundScheduler();

            await Threaded.Run(threads, () => {
                for (int i = 0; i < loopIterations; i++)
                {
                    // use the arg overload
                    lfu.GetOrAdd(i + 1, (i, s) => i.ToString(), "Foo");
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentGetAsyncWithArgCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateWithBackgroundScheduler();

            await Threaded.RunAsync(threads, async () => {
                for (int i = 0; i < loopIterations; i++)
                {
                    // use the arg overload
                    await lfu.GetOrAddAsync(i + 1, (i, s) => Task.FromResult(i.ToString()), "Foo");
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentGetAndUpdateCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateWithBackgroundScheduler();

            await Threaded.Run(threads, () => {
                for (int i = 0; i < loopIterations; i++)
                {
                    lfu.TryUpdate(i + 1, i.ToString());
                    lfu.GetOrAdd(i + 1, i => i.ToString());
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenSoakConcurrentGetAndRemoveCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateWithBackgroundScheduler();

            await Threaded.Run(threads, () => {
                for (int i = 0; i < loopIterations; i++)
                {
                    lfu.TryRemove(i + 1);
                    lfu.GetOrAdd(i + 1, i => i.ToString());
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentGetAndRemoveKvpCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateWithBackgroundScheduler();

            await Threaded.Run(threads, () => {
                for (int i = 0; i < loopIterations; i++)
                {
                    lfu.TryRemove(new KeyValuePair<int, string>(i + 1, (i + 1).ToString()));
                    lfu.GetOrAdd(i + 1, i => i.ToString());
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Fact]
        public async Task ThreadedVerifyMisses()
        {
            // buffer size is 1, this will cause dropped writes on some threads where the buffer is full
            var cache = new ConcurrentLfu<int, string>(1, 20, new NullScheduler(), EqualityComparer<int>.Default);

            await Threaded.Run(threads, i =>
            {
                Func<int, string> func = x => x.ToString();

                int start = i * loopIterations;

                for (int j = start; j < start + loopIterations; j++)
                {
                    cache.GetOrAdd(j, func);
                }
            });

            var samplePercent = cache.Metrics.Value.Misses / (double)loopIterations / threads * 100;

            this.output.WriteLine($"Cache misses {cache.Metrics.Value.Misses} (sampled {samplePercent}%)");
            this.output.WriteLine($"Maintenance ops {cache.Scheduler.RunCount}");

            cache.Metrics.Value.Misses.Should().Be(loopIterations * threads);
            RunIntegrityCheck(cache, this.output);
        }

        private ConcurrentLfu<int, string> CreateWithBackgroundScheduler()
        {
            var scheduler = new BackgroundThreadScheduler();
            return new ConcurrentLfuBuilder<int, string>().WithCapacity(9).WithScheduler(scheduler).Build() as ConcurrentLfu<int, string>;
        }

        private async Task RunIntegrityCheckAsync(ConcurrentLfu<int, string> lfu, int iteration)
        {
            this.output.WriteLine($"iteration {iteration} keys={string.Join(" ", lfu.Keys)}");

            var scheduler = lfu.Scheduler as BackgroundThreadScheduler;
            scheduler.Dispose();
            await scheduler.Completion;

            RunIntegrityCheck(lfu, this.output);
        }

        private static void RunIntegrityCheck<K,V>(ConcurrentLfu<K,V> cache, ITestOutputHelper output)
        {
            new ConcurrentLfuIntegrityChecker<K, V, AccessOrderNode<K, V>, AccessOrderPolicy<K, V>>(cache.Core).Validate(output);
        }
    }

    internal class ConcurrentLfuIntegrityChecker<K, V, N, P>
        where N : LfuNode<K, V>
        where P : struct, INodePolicy<K, V, N>
    {
        private readonly ConcurrentLfuCore<K, V, N, P> cache;

        private readonly LfuNodeList<K, V> windowLru;
        private readonly LfuNodeList<K, V> probationLru;
        private readonly LfuNodeList<K, V> protectedLru;

        private readonly StripedMpscBuffer<N> readBuffer;
        private readonly MpscBoundedBuffer<N> writeBuffer;

        private static FieldInfo windowLruField = typeof(ConcurrentLfuCore<K, V, N, P>).GetField("windowLru", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo probationLruField = typeof(ConcurrentLfuCore<K, V, N, P>).GetField("probationLru", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo protectedLruField = typeof(ConcurrentLfuCore<K, V, N, P>).GetField("protectedLru", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo readBufferField = typeof(ConcurrentLfuCore<K, V, N, P>).GetField("readBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo writeBufferField = typeof(ConcurrentLfuCore<K, V, N, P>).GetField("writeBuffer", BindingFlags.NonPublic | BindingFlags.Instance);

        public ConcurrentLfuIntegrityChecker(ConcurrentLfuCore<K, V, N, P> cache)
        {
            this.cache = cache;

            // get lrus via reflection
            this.windowLru = (LfuNodeList<K, V>)windowLruField.GetValue(cache);
            this.probationLru = (LfuNodeList<K, V>)probationLruField.GetValue(cache);
            this.protectedLru = (LfuNodeList<K, V>)protectedLruField.GetValue(cache);

            this.readBuffer = (StripedMpscBuffer<N>)readBufferField.GetValue(cache);
            this.writeBuffer = (MpscBoundedBuffer<N>)writeBufferField.GetValue(cache);
        }

        public void Validate(ITestOutputHelper output)
        {
            cache.DoMaintenance();

            // buffers should be empty after maintenance
            this.readBuffer.Count.Should().Be(0);
            this.writeBuffer.Count.Should().Be(0);

            // all the items in the LRUs must exist in the dictionary.
            // no items should be marked as removed after maintenance has run
            VerifyLruInDictionary(this.windowLru, output);
            VerifyLruInDictionary(this.probationLru, output);
            VerifyLruInDictionary(this.protectedLru, output);

            // all the items in the dictionary must exist in the node list
            VerifyDictionaryInLrus();

            // cache must be within capacity
            cache.Count.Should().BeLessThanOrEqualTo(cache.Capacity, "capacity out of valid range");
        }

        private void VerifyLruInDictionary(LfuNodeList<K, V> lfuNodes, ITestOutputHelper output)
        {
            var node = lfuNodes.First;

            while (node != null)
            {
                lock (node)
                {
                    node.WasRemoved.Should().BeFalse();
                    node.WasDeleted.Should().BeFalse();
                }

                cache.TryGet(node.Key, out _).Should().BeTrue($"Orphaned node with key {node.Key} detected.");

                node = node.Next;
            }
        }

        private void VerifyDictionaryInLrus()
        {
            foreach (var kvp in this.cache)
            {
                var exists = Exists(kvp, this.windowLru) || Exists(kvp, this.probationLru) || Exists(kvp, this.protectedLru);
                exists.Should().BeTrue($"key {kvp.Key} must exist in LRU lists");
            }
        }

        private static bool Exists(KeyValuePair<K, V> kvp, LfuNodeList<K, V> lfuNodes)
        {
            var node = lfuNodes.First;

            while (node != null)
            {
                if (EqualityComparer<K>.Default.Equals(node.Key, kvp.Key))
                {
                    return true;
                }

                node = node.Next;
            }

            return false;
        }
    }
}
