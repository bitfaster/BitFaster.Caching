using System.Collections.Generic;
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
                lru.Count.Should().BeCloseTo(9, 1);
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
                lru.Count.Should().BeCloseTo(9, 1);
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
                lru.Count.Should().BeCloseTo(9, 1);
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
                lru.Count.Should().BeCloseTo(9, 1);
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
        public async Task WhenSoakConcurrentGetAndUpdateCacheEndsInConsistentState()
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

        private void RunIntegrityCheck()
        {
            new ConcurrentLruIntegrityChecker<int, string, LruItem<int, string>, LruPolicy<int, string>, TelemetryPolicy<int, string>>(this.lru).Validate();
        }
    }
}
