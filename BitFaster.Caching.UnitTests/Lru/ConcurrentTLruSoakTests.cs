using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lru
{
    [Collection("Soak")]
    public class ConcurrentTLruSoakTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private const int hotCap = 33;
        private const int warmCap = 33;
        private const int coldCap = 33;
        private static readonly ICapacityPartition capacity = new EqualCapacityPartition(hotCap + warmCap + coldCap);

        private ConcurrentTLru<int, string> lru = new ConcurrentTLru<int, string>(1, capacity, EqualityComparer<int>.Default, TimeSpan.FromMilliseconds(10));

        public ConcurrentTLruSoakTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Theory]
        [Repeat(10)]
        public async Task WhenSoakConcurrentGetCacheEndsInConsistentState(int iteration)
        {
            await Threaded.Run(4, () => {
                for (int j = 0; j < 100000; j++)
                {
                    for (int i = 0; i < lru.Capacity; i++)
                    {
                        lru.GetOrAdd(i + 1, i => i.ToString());
                    }
                }
            });

            this.testOutputHelper.WriteLine($"iteration{iteration}: {lru.HotCount} {lru.WarmCount} {lru.ColdCount}");
            this.testOutputHelper.WriteLine(string.Join(" ", lru.Keys));

            RunIntegrityCheck();
        }

        private void RunIntegrityCheck()
        {
            new ConcurrentLruIntegrityChecker<int, string, LongTickCountLruItem<int, string>, TLruLongTicksPolicy<int, string>, TelemetryPolicy<int, string>>(this.lru).Validate();
        }
    }
}
