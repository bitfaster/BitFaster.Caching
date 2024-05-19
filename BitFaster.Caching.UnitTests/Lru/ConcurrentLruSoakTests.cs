using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lru
{
    [Collection("Soak")]
    public class ConcurrentLruSoakTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private const int hotCap = 3;
        private const int warmCap = 3;
        private const int coldCap = 3;
        private static readonly ICapacityPartition capacity = new EqualCapacityPartition(hotCap + warmCap + coldCap);

        private ConcurrentLru<int, string> lru = new ConcurrentLru<int, string>(1, capacity, EqualityComparer<int>.Default);

        public ConcurrentLruSoakTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task WhenSoakConcurrentGetCacheEndsInConsistentState()
        {
            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        lru.GetOrAdd(i + 1, i => i.ToString());
                    }
                });

                this.testOutputHelper.WriteLine($"{lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
                this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

                // allow +/- 1 variance for capacity
                lru.Count.Should().BeInRange(7, 10);
                RunIntegrityCheck();
            }
        }

        [Fact]
        public async Task WhenSoakConcurrentGetAsyncCacheEndsInConsistentState()
        {
            for (int i = 0; i < 10; i++)
            {
                await Threaded.RunAsync(4, async () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        await lru.GetOrAddAsync(i + 1, i => Task.FromResult(i.ToString()));
                    }
                });

                this.testOutputHelper.WriteLine($"{lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
                this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

                // allow +/- 1 variance for capacity
                lru.Count.Should().BeInRange(7, 10);
                RunIntegrityCheck();
            }
        }

        [Fact]
        public async Task WhenSoakConcurrentGetWithArgCacheEndsInConsistentState()
        {
            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        // use the arg overload
                        lru.GetOrAdd(i + 1, (i, s) => i.ToString(), "Foo");
                    }
                });

                this.testOutputHelper.WriteLine($"{lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
                this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

                // allow +/- 1 variance for capacity
                lru.Count.Should().BeInRange(7, 10);
                RunIntegrityCheck();
            }
        }

        [Fact]
        public async Task WhenSoakConcurrentGetAsyncWithArgCacheEndsInConsistentState()
        {
            for (int i = 0; i < 10; i++)
            {
                await Threaded.RunAsync(4, async () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        // use the arg overload
                        await lru.GetOrAddAsync(i + 1, (i, s) => Task.FromResult(i.ToString()), "Foo");
                    }
                });

                this.testOutputHelper.WriteLine($"{lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
                this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

                // allow +/- 1 variance for capacity
                lru.Count.Should().BeInRange(7, 10);
                RunIntegrityCheck();
            }
        }

        [Fact]
        public async Task WhenSoakConcurrentGetAndRemoveCacheEndsInConsistentState()
        {
            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        lru.TryRemove(i + 1);
                        lru.GetOrAdd(i + 1, i => i.ToString());
                    }
                });

                this.testOutputHelper.WriteLine($"{lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
                this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

                RunIntegrityCheck();
            }
        }

        [Fact]
        public async Task WhenSoakConcurrentGetAndRemoveKvpCacheEndsInConsistentState()
        {
            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        lru.TryRemove(new KeyValuePair<int, string>(i + 1, (i + 1).ToString()));
                        lru.GetOrAdd(i + 1, i => i.ToString());
                    }
                });

                this.testOutputHelper.WriteLine($"{lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
                this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

                RunIntegrityCheck();
            }
        }

        [Fact]
        public async Task WhenSoakConcurrentGetAndUpdateRefTypeCacheEndsInConsistentState()
        {
            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        lru.TryUpdate(i + 1, i.ToString());
                        lru.GetOrAdd(i + 1, i => i.ToString());
                    }
                });

                this.testOutputHelper.WriteLine($"{lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
                this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

                RunIntegrityCheck();
            }
        }

        [Fact]
        public async Task WhenSoakConcurrentGetAndUpdateValueTypeCacheEndsInConsistentState()
        {
            var lruVT = new ConcurrentLru<int, Guid>(1, capacity, EqualityComparer<int>.Default);

            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, () => {
                    var b = new byte[8];
                    for (int i = 0; i < 100000; i++)
                    {
                        lruVT.TryUpdate(i + 1, new Guid(i, 0, 0, b));
                        lruVT.GetOrAdd(i + 1, x => new Guid(x, 0, 0, b));
                    }
                });

                this.testOutputHelper.WriteLine($"{lruVT.HotCount} {lruVT.WarmCount} {lruVT.ColdCount}");
                this.testOutputHelper.WriteLine(string.Join(" ", lruVT.Keys));

                new ConcurrentLruIntegrityChecker<int, Guid, LruItem<int, Guid>, LruPolicy<int, Guid>, TelemetryPolicy<int, Guid>>(lruVT).Validate();
            }
        }

        [Fact]
        public async Task WhenSoakConcurrentGetAndAddCacheEndsInConsistentState()
        {
            for (int i = 0; i < 10; i++)
            {
                await Threaded.Run(4, () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        lru.AddOrUpdate(i + 1, i.ToString());
                        lru.GetOrAdd(i + 1, i => i.ToString());
                    }
                });

                this.testOutputHelper.WriteLine($"{lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
                this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

                RunIntegrityCheck();
            }
        }

        [Fact]
        public async Task WhenAddingCacheSizeItemsNothingIsEvicted()
        {
            const int size = 1024;

            var cache = new ConcurrentLruBuilder<int, int>()
                .WithMetrics()
                .WithCapacity(size)
                .Build();

            await Threaded.Run(4, () =>
            {
                for (int i = 0; i < size; i++)
                {
                    cache.GetOrAdd(i, k => k);
                }
            });

            cache.Metrics.Value.Evicted.Should().Be(0);
        }

        [Theory]
        [Repeat(10)]
        public async Task WhenConcurrentGetAndClearCacheEndsInConsistentState(int iteration)
        {
            await Threaded.Run(4, r => {
                for (int i = 0; i < 100000; i++)
                {
                    // clear 6,250 times per 1_000_000 iters
                    if (r == 0 && (i & 15) == 15)
                    {
                        lru.Clear();
                    }

                    lru.GetOrAdd(i + 1, i => i.ToString());
                }
            });

            this.testOutputHelper.WriteLine($"{iteration} {lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
            this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

            RunIntegrityCheck();
        }

        [Theory]
        [Repeat(10)]
        public async Task WhenConcurrentGetAndClearDuringWarmupCacheEndsInConsistentState(int iteration)
        {
            await Threaded.Run(4, r => {
                for (int i = 0; i < 100000; i++)
                {
                    // clear 25,000 times per 1_000_000 iters
                    // capacity is 9, so we will try to clear before warmup is done
                    if (r == 0 && (i & 3) == 3)
                    {
                        lru.Clear();
                    }

                    lru.GetOrAdd(i + 1, i => i.ToString());
                }
            });

            this.testOutputHelper.WriteLine($"{iteration} {lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
            this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

            RunIntegrityCheck();
        }

        private void RunIntegrityCheck()
        {
            new ConcurrentLruIntegrityChecker<int, string, LruItem<int, string>, LruPolicy<int, string>, TelemetryPolicy<int, string>>(this.lru).Validate();
        }
    }
}
