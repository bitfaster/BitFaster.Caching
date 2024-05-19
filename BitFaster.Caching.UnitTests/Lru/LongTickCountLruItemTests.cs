using System.Collections.Concurrent;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class LongTickCountLruItemTests
    {
        // Validate that using the base class Equals/HashCode we can update ConcurrentDictionary
        // This replicates ConcurrentLruCore.TryUpdate for non-write atomic updates.
        [Fact]
        public void WhenInConcurrentDictionaryCanBeReplaced()
        { 
            var item1 = new LongTickCountLruItem<int, int>(1, 2, 3);
            var item2 = new LongTickCountLruItem<int, int>(2, 1, 0);

            var d = new ConcurrentDictionary<int, LongTickCountLruItem<int, int>>();
            d.TryAdd(1, item1);

            d.TryUpdate(1, item2, item1).Should().BeTrue();
        }
    }
}
