using System.Linq;
using System.Threading.Tasks;
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
            var cache = new ConcurrentLruBuilder<int, int>().WithAtomicGetOrAdd().WithCapacity(1024).Build();

            var counters = new int[4];

            await Threaded.Run(4, (r) =>
            {
                for (int i = 0; i < 1024; i++)
                {
                    cache.GetOrAdd(i, k => { counters[r]++; return k; });
                }
            });

            counters.Sum(x => x).Should().Be(1024);
        }
    }
}
