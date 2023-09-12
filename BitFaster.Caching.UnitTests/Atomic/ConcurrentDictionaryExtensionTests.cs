
using System.Collections.Concurrent;
using BitFaster.Caching.Atomic;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class ConcurrentDictionaryExtensionTests
    {
        private ConcurrentDictionary<int, AtomicFactory<int, int>> dictionary = new ConcurrentDictionary<int, AtomicFactory<int, int>>();

        [Fact]
        public void GetOrAdd()
        {
            dictionary.GetOrAdd(1, k => k);

            dictionary.TryGetValue(1, out int value).Should().BeTrue();
            value.Should().Be(1);
        }
    }
}
