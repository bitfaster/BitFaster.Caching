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
        private readonly ITestOutputHelper output;
        public ConcurrentLfuSoakTests(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
        }

        [Theory]
        [Repeat(10)]
        public async Task WhenConcurrentGetCacheEndsInConsistentState(int iteration)
        {
            var lfu = new ConcurrentLfu<int, int>(9);

            await Threaded.Run(4, () => {
                for (int i = 0; i < 100000; i++)
                {
                    lfu.GetOrAdd(i + 1, i => i);
                }
            });

            this.output.WriteLine($"iteration {iteration} keys={string.Join(" ", lfu.Keys)}");

            // allow +/- 1 variance for capacity
            lfu.Count.Should().BeInRange(7, 10);
            RunIntegrityCheck(lfu);
        }

        [Fact]
        public async Task ThreadedVerifyMisses()
        {
            // buffer size is 1, this will cause dropped writes on some threads where the buffer is full
            var cache = new ConcurrentLfu<int, int>(1, 20, new NullScheduler(), EqualityComparer<int>.Default);

            int threads = 4;
            int iterations = 100_000;

            await Threaded.Run(threads, i =>
            {
                Func<int, int> func = x => x;

                int start = i * iterations;

                for (int j = start; j < start + iterations; j++)
                {
                    cache.GetOrAdd(j, func);
                }
            });

            var samplePercent = cache.Metrics.Value.Misses / (double)iterations / threads * 100;

            this.output.WriteLine($"Cache misses {cache.Metrics.Value.Misses} (sampled {samplePercent}%)");
            this.output.WriteLine($"Maintenance ops {cache.Scheduler.RunCount}");

            cache.Metrics.Value.Misses.Should().Be(iterations * threads);
            RunIntegrityCheck(cache);
        }

        private void RunIntegrityCheck<K,V>(ConcurrentLfu<K,V> cache)
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
                exists.Should().BeTrue();
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
