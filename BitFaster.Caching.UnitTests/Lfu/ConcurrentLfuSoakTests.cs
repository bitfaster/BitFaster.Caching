using System;
using System.Collections.Generic;
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
        private const int soakIterations = 10;
        private const int threads = 4;
        private const int loopIterations = 100_000;

        private readonly ITestOutputHelper output;
        public ConcurrentLfuSoakTests(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
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
            RunIntegrityCheck(cache);
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

            RunIntegrityCheck(lfu);
        }


        private static void RunIntegrityCheck<K,V>(ConcurrentLfu<K,V> cache)
        {
            new ConcurrentLfuIntegrityChecker<K, V>(cache).Validate();
        }
    }

    public class ConcurrentLfuIntegrityChecker<K, V>
    {
        private readonly ConcurrentLfu<K, V> cache;

        private readonly LfuNodeList<K, V> windowLru;
        private readonly LfuNodeList<K, V> probationLru;
        private readonly LfuNodeList<K, V> protectedLru;

        private readonly StripedMpscBuffer<LfuNode<K, V>> readBuffer;
        private readonly MpscBoundedBuffer<LfuNode<K, V>> writeBuffer;

        private static FieldInfo windowLruField = typeof(ConcurrentLfu<K, V>).GetField("windowLru", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo probationLruField = typeof(ConcurrentLfu<K, V>).GetField("probationLru", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo protectedLruField = typeof(ConcurrentLfu<K, V>).GetField("protectedLru", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo readBufferField = typeof(ConcurrentLfu<K, V>).GetField("readBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo writeBufferField = typeof(ConcurrentLfu<K, V>).GetField("writeBuffer", BindingFlags.NonPublic | BindingFlags.Instance);

        public ConcurrentLfuIntegrityChecker(ConcurrentLfu<K, V> cache)
        {
            this.cache = cache;

            // get lrus via reflection
            this.windowLru = (LfuNodeList<K, V>)windowLruField.GetValue(cache);
            this.probationLru = (LfuNodeList<K, V>)probationLruField.GetValue(cache);
            this.protectedLru = (LfuNodeList<K, V>)protectedLruField.GetValue(cache);

            this.readBuffer = (StripedMpscBuffer<LfuNode<K, V>>)readBufferField.GetValue(cache);
            this.writeBuffer = (MpscBoundedBuffer<LfuNode<K, V>>)writeBufferField.GetValue(cache);
        }

        public void Validate()
        {
            cache.DoMaintenance();

            // buffers should be empty after maintenance
            this.readBuffer.Count.Should().Be(0);
            this.writeBuffer.Count.Should().Be(0);

            // all the items in the LRUs must exist in the dictionary.
            // no items should be marked as removed after maintenance has run
            VerifyLruInDictionary(this.windowLru);
            VerifyLruInDictionary(this.probationLru);
            VerifyLruInDictionary(this.protectedLru);

            // all the items in the dictionary must exist in the node list
            VerifyDictionaryInLrus();

            // cache must be within capacity
            cache.Count.Should().BeLessThanOrEqualTo(cache.Capacity, "capacity out of valid range");
        }

        private void VerifyLruInDictionary(LfuNodeList<K, V> lfuNodes)
        {
            var node = lfuNodes.First;

            while (node != null) 
            {
                node.WasRemoved.Should().BeFalse();
                node.WasDeleted.Should().BeFalse();
                cache.TryGet(node.Key, out _).Should().BeTrue();

                node = node.Next;
            }
        }

        private void VerifyDictionaryInLrus()
        {
            foreach (var kvp in this.cache)
            {
                var exists = Exists(kvp, this.windowLru) || Exists(kvp, this.probationLru) || Exists(kvp, this.protectedLru);
                exists.Should().BeTrue($"key {kvp.Key} should exist in LRU lists");
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
