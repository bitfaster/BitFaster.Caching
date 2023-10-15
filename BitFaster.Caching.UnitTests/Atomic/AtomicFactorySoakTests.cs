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
            const int threads = 4;
            const int items = 1024;
            var dictionary = new ConcurrentDictionary<int, AtomicFactory<int, int>>(concurrencyLevel: threads, capacity: items);
            var counters = new int[threads];

            await Threaded.Run(threads, (r) =>
            {
                for (int i = 0; i < items; i++)
                {
                    dictionary.GetOrAdd(i, k => { counters[r]++; return k; });
                }
            });

            counters.Sum(x => x).Should().Be(items);
        }
    }
}
