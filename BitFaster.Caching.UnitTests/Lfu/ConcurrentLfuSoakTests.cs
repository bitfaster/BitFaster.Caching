using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
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
        public async Task WhenWeightedConcurrentGetCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateWeightedWithBackground(9, (k, v) => 1);

            await Threaded.Run(threads, () =>
            {
                for (int i = 0; i < loopIterations; i++)
                {
                    lfu.GetOrAdd(i + 1, i => i);
                }
            });

            await RunWeightedIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task ConcurrentGetOrAddWithRandomWeightsAndProtectedEntriesPreservesIntegrity(int iteration)
        {
            const int capacity = 100;
            const int keyCount = 128;
            const int protectedKeyCount = 4;

            var random = new Random(iteration);
            var weights = new int[threads][];

            for (int thread = 0; thread < threads; thread++)
            {
                weights[thread] = new int[keyCount];
                for (int key = 0; key < keyCount; key++)
                {
                    // Keep the hot set below half the weight budget so it can be promoted.
                    int minWeight = key < protectedKeyCount ? 1 : 0;
                    int maxWeight = key < protectedKeyCount ? capacity / (2 * protectedKeyCount) : capacity;
                    weights[thread][key] = random.Next(minWeight, maxWeight + 1);
                }
            }

            var cache = CreateWeightedWithBackground(capacity, (k, v) => v);

            var eviction = cache.Core.policy.eviction;
            int evictionsWithProtectedEntries = 0;

            cache.Events.Value.ItemRemoved += (sender, args) =>
            {
                if (args.Reason == ItemRemovedReason.Evicted && eviction.ProtectedWeightedSize > 0)
                {
                    Interlocked.Increment(ref evictionsWithProtectedEntries);
                }
            };

            Func<int, int> warmupFactory = key => weights[0][key];
            for (int key = 0; key < protectedKeyCount; key++)
            {
                cache.GetOrAdd(key, warmupFactory);
            }
            cache.DoMaintenance();
            for (int key = 0; key < protectedKeyCount; key++)
            {
                cache.GetOrAdd(key, warmupFactory);
            }
            cache.DoMaintenance();
            eviction.ProtectedWeightedSize.Should().BeGreaterThan(0);

            await Threaded.Run(threads, thread =>
            {
                var threadWeights = weights[thread];
                Func<int, int> valueFactory = key => threadWeights[key];
                for (int i = 0; i < loopIterations; i++)
                {
                    int request = i / 2;
                    int key = i % 2 == 0 ? request % protectedKeyCount : request % keyCount;
                    cache.GetOrAdd(key, valueFactory);
                }
            });

            await RunWeightedIntegrityCheckAsync(cache, iteration);

            this.output.WriteLine($"iteration {iteration}, evictions with protected entries={evictionsWithProtectedEntries}");
            evictionsWithProtectedEntries.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ConcurrentWeightedUpdatesRemovalsAndTrimsPreserveAccounting(bool withExpiry)
        {
            var scheduler = new BackgroundThreadScheduler();
            var builder = new ConcurrentLfuBuilder<int, int>()
                .WithCapacity(100)
                .WithScheduler(scheduler)
                .WithWeightedEntries(new TestWeightCalculator<int, int>((k, v) => v))
                .WithEvents();

            if (withExpiry)
            {
                var cache = (WeightedConcurrentLfu<int, int, WeightedTimeOrderNode<int, int>, WeightedExpireAfterPolicy<int, int, EventPolicy<int, int>>>)
                    builder.WithExpireAfterWrite(TimeSpan.FromMinutes(10)).Build();
                await VerifyWeightedConcurrency(cache.Core, scheduler);
            }
            else
            {
                var cache = (WeightedConcurrentLfu<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, EventPolicy<int, int>>>)builder.Build();
                await VerifyWeightedConcurrency(cache.Core, scheduler);
            }
        }

        private async Task VerifyWeightedConcurrency<N, P>(ConcurrentLfuCore<int, int, N, P, EventPolicy<int, int>> core, BackgroundThreadScheduler scheduler)
            where N : LfuNode<int, int>
            where P : struct, INodePolicy<int, int, N, EventPolicy<int, int>>, INodeWeight<N>
        {
            try
            {
                await Threaded.Run(threads, () =>
                {
                    for (int i = 0; i < loopIterations; i++)
                    {
                        int key = (i + Thread.CurrentThread.ManagedThreadId) % 128;
                        core.AddOrUpdate(key, i % 8);
                        core.TryUpdate(key, (i + 1) % 12);
                        core.TryGet(key, out _);
                        if (i % 3 == 0)
                        {
                            core.TryRemove(key);
                        }
                        if (i % 1000 == 0)
                        {
                            core.Trim(3);
                        }
                    }
                });
            }
            finally
            {
                scheduler.Dispose();
                await scheduler.Completion;
            }

            new ConcurrentLfuIntegrityChecker<int, int, N, P, EventPolicy<int, int>>(core).Validate(this.output, enforceCountCapacity: false);
            core.DoMaintenance();
            WeightedConcurrentLfuCoreTests.AssertWeightedState(core);
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

            await Threaded.Run(threads, () =>
            {
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

            await Threaded.RunAsync(threads, async () =>
            {
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

            await Threaded.Run(threads, () =>
            {
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

            await Threaded.RunAsync(threads, async () =>
            {
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

            await Threaded.Run(threads, () =>
            {
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

            await Threaded.Run(threads, () =>
            {
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

            await Threaded.Run(threads, () =>
            {
                for (int i = 0; i < loopIterations; i++)
                {
                    lfu.TryRemove(new KeyValuePair<int, string>(i + 1, (i + 1).ToString()));
                    lfu.GetOrAdd(i + 1, i => i.ToString());
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentTryGetAddOrUpdateAndTrimCacheEndsInConsistentState(int iteration)
        {
            const long trimAfter = 100;
            string value = "x";

            var trimmingCache = new TrimmingLfuCache(maxItems: 1_000_000, trimAfter: trimAfter);
            Parallel.For(0, 5000, i => trimmingCache.AddWithTrim("x" + i, value));
            trimmingCache.AddWithTrim("y", value);

            await RunIntegrityCheckAsync(trimmingCache._cache, iteration);
        }

#if NET9_0_OR_GREATER
        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentAlternateLookupGetCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateStringWithBackgroundScheduler();
            var alternate = lfu.GetAlternateLookup<ReadOnlySpan<char>>();

            await Threaded.Run(threads, () =>
            {
                for (int i = 0; i < loopIterations; i++)
                {
                    string key = (i + 1).ToString();
                    alternate.GetOrAdd(key.AsSpan(), static keySpan => keySpan.ToString());
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentAlternateLookupGetWithArgCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateStringWithBackgroundScheduler();
            var alternate = lfu.GetAlternateLookup<ReadOnlySpan<char>>();

            await Threaded.Run(threads, () =>
            {
                for (int i = 0; i < loopIterations; i++)
                {
                    string key = (i + 1).ToString();
                    alternate.GetOrAdd(key.AsSpan(), static (keySpan, prefix) => prefix + keySpan.ToString(), "prefix-");
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentAlternateLookupGetAndRemoveCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateStringWithBackgroundScheduler();
            var alternate = lfu.GetAlternateLookup<ReadOnlySpan<char>>();

            await Threaded.Run(threads, () =>
            {
                for (int i = 0; i < loopIterations; i++)
                {
                    string key = (i + 1).ToString();
                    alternate.TryRemove(key.AsSpan(), out _, out _);
                    alternate.AddOrUpdate(key.AsSpan(), key);
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentAsyncAlternateLookupGetOrAddAsyncCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateStringWithBackgroundScheduler();
            var alternate = lfu.GetAsyncAlternateLookup<ReadOnlySpan<char>>();

            await Threaded.RunAsync(threads, async () =>
            {
                var key = new char[8];
                for (int i = 0; i < loopIterations; i++)
                {
                    (i + 1).TryFormat(key, out int written);
                    await alternate.GetOrAddAsync(key.AsSpan().Slice(0, written), static keySpan => Task.FromResult(keySpan.ToString()));
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }

        [Theory]
        [Repeat(soakIterations)]
        public async Task WhenConcurrentAsyncAlternateLookupGetOrAddAsyncWithArgCacheEndsInConsistentState(int iteration)
        {
            var lfu = CreateStringWithBackgroundScheduler();
            var alternate = lfu.GetAsyncAlternateLookup<ReadOnlySpan<char>>();

            await Threaded.RunAsync(threads, async () =>
            {
                var key = new char[8];
                for (int i = 0; i < loopIterations; i++)
                {
                    (i + 1).TryFormat(key, out int written);
                    await alternate.GetOrAddAsync(key.AsSpan().Slice(0, written), static (keySpan, prefix) => Task.FromResult(prefix + keySpan.ToString()), "prefix-");
                }
            });

            await RunIntegrityCheckAsync(lfu, iteration);
        }
#endif

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

        [Fact]
        public async Task WhenConcurrentUpdateAndRemoveKvp()
        {
            var cache = new ConcurrentLfu<int, string>(1, 20, new BackgroundThreadScheduler(), EqualityComparer<int>.Default);
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            var removal = Task.Run(() =>
            {
                while (!tcs.Task.IsCompleted)
                {
                    cache.TryRemove(new KeyValuePair<int, string>(5, "x"));
                }
            });

            for (var i = 0; i < 100_000; i++)
            {
                cache.AddOrUpdate(5, "a");
                cache.TryGet(5, out _).Should().BeTrue("key 'a' should not be deleted");
                cache.AddOrUpdate(5, "x");
            }

            tcs.SetResult(int.MaxValue);

            await removal;
        }

        // This test will run forever if there is a live lock.
        // Since the cache bookkeeping has some overhead, it is harder to provoke
        // spinning inside the reader thread compared to LruItemSoakTests.DetectTornStruct.
        [Theory]
        [Repeat(10)]
        public async Task WhenValueIsBigStructNoLiveLock(int _)
        {
            using var source = new CancellationTokenSource();
            var started = new TaskCompletionSource<bool>();
            var cache = new ConcurrentLfu<int, Guid>(1, 20, new BackgroundThreadScheduler(), EqualityComparer<int>.Default);

            var setTask = Task.Run(() => Setter(cache, source.Token, started));
            await started.Task;
            Checker(cache, source);

            await setTask;
        }

        private void Setter(ICache<int, Guid> cache, CancellationToken cancelToken, TaskCompletionSource<bool> started)
        {
            started.SetResult(true);

            while (true)
            {
                cache.AddOrUpdate(1, Guid.NewGuid());
                cache.AddOrUpdate(1, Guid.NewGuid());

                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private void Checker(ICache<int, Guid> cache, CancellationTokenSource source)
        {
            for (int count = 0; count < 100_000; ++count)
            {
                cache.TryGet(1, out _);
            }

            source.Cancel();
        }

        private ConcurrentLfu<int, string> CreateWithBackgroundScheduler()
        {
            var scheduler = new BackgroundThreadScheduler();
            return new ConcurrentLfuBuilder<int, string>().WithCapacity(9).WithEvents().WithScheduler(scheduler).Build() as ConcurrentLfu<int, string>;
        }

        private ConcurrentLfu<string, string> CreateStringWithBackgroundScheduler()
        {
            var scheduler = new BackgroundThreadScheduler();
            return new ConcurrentLfu<string, string>(1, 9, scheduler, StringComparer.Ordinal);
        }

        private static WeightedConcurrentLfu<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, EventPolicy<int, int>>> CreateWeightedWithBackground(int capacity, Func<int, int, int> calculate)
        {
            var scheduler = new BackgroundThreadScheduler();
            var lfu = (WeightedConcurrentLfu<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, EventPolicy<int, int>>>)
                new ConcurrentLfuBuilder<int, int>()
                    .WithCapacity(capacity)
                    .WithConcurrencyLevel(threads)
                    .WithScheduler(scheduler)
                    .WithWeightedEntries(new TestWeightCalculator<int, int>(calculate))
                    .WithEvents()
                    .Build();
            return lfu;
        }

        private async Task RunIntegrityCheckAsync(ConcurrentLfu<int, string> lfu, int iteration)
        {
            Log(lfu, iteration, "before");

            if (lfu.Scheduler is BackgroundThreadScheduler scheduler)
            {
                scheduler.Dispose();
                await scheduler.Completion;
            }

            Log(lfu, iteration, "after");

            RunIntegrityCheck(lfu, this.output);
        }

        private async Task RunIntegrityCheckAsync(ConcurrentLfu<string, string> lfu, int iteration)
        {
            Log(lfu, iteration, "before");
            if (lfu.Scheduler is BackgroundThreadScheduler scheduler)
            {
                scheduler.Dispose();
                await scheduler.Completion;
            }
            Log(lfu, iteration, "after");

            RunIntegrityCheck(lfu, this.output);
        }

        private async Task RunWeightedIntegrityCheckAsync(WeightedConcurrentLfu<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, EventPolicy<int, int>>> lfu, int iteration)
        {
            Log(lfu, iteration, "before");
            if (lfu.Scheduler is BackgroundThreadScheduler s)
            {
                s.Dispose();
                await s.Completion;
            }

            Log(lfu, iteration, "after");
            var core = lfu.Core;
            new ConcurrentLfuIntegrityChecker<int, int, WeightedAccessOrderNode<int, int>, WeightedAccessOrderPolicy<int, int, EventPolicy<int, int>>, EventPolicy<int, int>>(core)
                .Validate(this.output, enforceCountCapacity: false);

            WeightedConcurrentLfuCoreTests.AssertWeightedState(core);
            var eviction = lfu.Core.policy.eviction;
            eviction.WindowWeightedSize.Should().BeLessThanOrEqualTo(eviction.Capacity.Window);
            eviction.ProtectedWeightedSize.Should().BeLessThanOrEqualTo(eviction.Capacity.Protected);
        }

        private void Log<K>(ConcurrentLfu<K, string> lfu, int iteration, string stage)
        {
            this.output.WriteLine($"{stage} iteration {iteration} keys={string.Join(" ", lfu.Keys)}");
#if DEBUG
            this.output.WriteLine(lfu.FormatLfuString());
#endif
        }

        private void Log<K>(WeightedConcurrentLfu<K, int, WeightedAccessOrderNode<K, int>, WeightedAccessOrderPolicy<K, int, EventPolicy<K, int>>> lfu, int iteration, string stage)
        {
            this.output.WriteLine($"{stage} iteration {iteration} keys={string.Join(" ", lfu.Keys)}");
#if DEBUG
            this.output.WriteLine(lfu.FormatLfuString());
#endif
        }

        private static void RunIntegrityCheck<K, V>(ConcurrentLfu<K, V> cache, ITestOutputHelper output)
        {
            new ConcurrentLfuIntegrityChecker<K, V, AccessOrderNode<K, V>, AccessOrderPolicy<K, V, EventPolicy<K, V>>, EventPolicy<K, V>>(cache.Core).Validate(output);
        }
    }

    internal class ConcurrentLfuIntegrityChecker<K, V, N, P, E>
        where N : LfuNode<K, V>
        where P : struct, INodePolicy<K, V, N, E>
        where E : struct, IEventPolicy<K, V>
    {
        private readonly ConcurrentLfuCore<K, V, N, P, E> cache;
        private readonly ConcurrentDictionary<K, N> dictionary;

#if NET9_0_OR_GREATER
        private readonly Lock maintenanceLock;
#else
        private readonly object maintenanceLock;
#endif

        private readonly LfuNodeList<K, V> windowLru;
        private readonly LfuNodeList<K, V> probationLru;
        private readonly LfuNodeList<K, V> protectedLru;

        private readonly StripedMpscBuffer<N> readBuffer;
        private readonly MpscBoundedBuffer<N> writeBuffer;

        private static FieldInfo dictionaryField = typeof(ConcurrentLfuCore<K, V, N, P, E>).GetField("dictionary", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo lockField = typeof(ConcurrentLfuCore<K, V, N, P, E>).GetField("maintenanceLock", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo readBufferField = typeof(ConcurrentLfuCore<K, V, N, P, E>).GetField("readBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo writeBufferField = typeof(ConcurrentLfuCore<K, V, N, P, E>).GetField("writeBuffer", BindingFlags.NonPublic | BindingFlags.Instance);

        public ConcurrentLfuIntegrityChecker(ConcurrentLfuCore<K, V, N, P, E> cache)
        {
            this.cache = cache;

            this.dictionary = (ConcurrentDictionary<K, N>)dictionaryField.GetValue(cache);

#if NET9_0_OR_GREATER
            this.maintenanceLock = (Lock)lockField.GetValue(cache);
#else
            this.maintenanceLock = lockField.GetValue(cache);
#endif

            this.windowLru = cache.policy.Eviction.Window;
            this.probationLru = cache.policy.Eviction.Probation;
            this.protectedLru = cache.policy.Eviction.Protected;

            this.readBuffer = (StripedMpscBuffer<N>)readBufferField.GetValue(cache);
            this.writeBuffer = (MpscBoundedBuffer<N>)writeBufferField.GetValue(cache);
        }

        public void Validate(ITestOutputHelper output, bool enforceCountCapacity = true)
        {
            cache.DoMaintenance();

            if (cache.Scheduler.LastException.HasValue)
            {
                output.WriteLine($"Last scheduler exception {cache.Scheduler.LastException.Value}");
                cache.Scheduler.LastException.Should().BeNull("scheduler should not have thrown");
            }

            lock (this.maintenanceLock)
            {
#if DEBUG
                output.WriteLine("LRUs under lock:");
                output.WriteLine(cache.FormatLfuString());
#endif

                // buffers should be empty after maintenance
                this.readBuffer.Count.Should().Be(0);
                this.writeBuffer.Count.Should().Be(0);

                AssertLruCountMatchesEnumeration(this.windowLru, "window");
                AssertLruCountMatchesEnumeration(this.protectedLru, "protected");
                AssertLruCountMatchesEnumeration(this.probationLru, "probation");

                // all the items in the LRUs must exist in the dictionary.
                // no items should be marked as removed after maintenance has run
                VerifyLruInDictionary(this.windowLru, output);
                VerifyLruInDictionary(this.probationLru, output);
                VerifyLruInDictionary(this.protectedLru, output);

                // all the items in the dictionary must exist in the node list
                VerifyDictionaryInLrus();
            }

            // cache must be within capacity
            if (enforceCountCapacity)
            {
                cache.Count.Should().BeLessThanOrEqualTo(cache.Capacity, "capacity out of valid range");
            }
        }

        private void AssertLruCountMatchesEnumeration(LfuNodeList<K, V> lfuNodes, string lruName)
        {
            int enumeratedCount = 0;
            int enumerationGuardLimit = Math.Max(this.cache.Capacity, this.dictionary.Count) + 1;

            foreach (var node in lfuNodes)
            {
                enumeratedCount++;
                enumeratedCount.Should().BeLessThanOrEqualTo(enumerationGuardLimit, $"{lruName} LRU enumeration exceeded the guard limit");
            }

            enumeratedCount.Should().Be(lfuNodes.Count, $"{lruName} LRU has a corrupted count");
        }

        private void VerifyLruInDictionary(LfuNodeList<K, V> lfuNodes, ITestOutputHelper output)
        {
            var node = lfuNodes.First;

            while (node != null)
            {
                node.WasRemoved.Should().BeFalse();
                node.WasDeleted.Should().BeFalse();

                // additional diagnbostics
                if (!cache.TryGet(node.Key, out _))
                {
                    output.WriteLine($"Orphaned node at {node.Position} with key {node.Key} and value {node.Value}.");
                    output.WriteLine($"Read buffer {cache.readBuffer.Count} write buffer {cache.writeBuffer.Count}.");
                }

                cache.TryGet(node.Key, out _).Should().BeTrue($"Orphaned node with key {node.Key} detected.");

                node = node.Next;
            }
        }

        private void VerifyDictionaryInLrus()
        {
            foreach (var kvp in this.dictionary)
            {
                var exists = Exists(kvp, this.windowLru) || Exists(kvp, this.probationLru) || Exists(kvp, this.protectedLru);
                exists.Should().BeTrue($"key {kvp.Key} in {kvp.Value.Position} removed: {kvp.Value.WasRemoved} deleted {kvp.Value.WasDeleted} must exist in LRU lists");
            }
        }

        private static bool Exists(KeyValuePair<K, N> kvp, LfuNodeList<K, V> lfuNodes)
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
