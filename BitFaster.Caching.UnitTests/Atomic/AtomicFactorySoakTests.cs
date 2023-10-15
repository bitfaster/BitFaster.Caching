using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using BitFaster.Caching.Atomic;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    [Collection("Soak")]
    public class AtomicFactorySoakTests
    {
        [Fact]
        public async Task WhenGetOrAddIsConcurrentValuesCreatedAtomically()
        {
            var cache = new ConcurrentLruBuilder<int, int>()
                .WithAtomicGetOrAdd()
                .WithMetrics()
                .WithCapacity(1024)
                .Build();

            var counters = new int[4];

            await Threaded.Run(4, (r) =>
            {
                for (int i = 0; i < 1024; i++)
                {
                    cache.GetOrAdd(i, k => { counters[r]++; return k; });
                }
            });

            cache.Metrics.Value.Evicted.Should().Be(0);
            counters.Sum(x => x).Should().Be(1024);
        }

        [Fact]
        public async Task WhenGetOrAddIsConcurrentValuesCreatedAtomically2()
        {
            var dictionary = new ConcurrentDictionary<int, AtomicFactory<int, int>>(4, 1024);

            var counters = new int[4];

            await Threaded.Run(4, (r) =>
            {
                for (int i = 0; i < 1024; i++)
                {
                    dictionary.GetOrAdd(i, k => { counters[r]++; return k; });
                }
            });

            counters.Sum(x => x).Should().Be(1024);
        }
    }
}
