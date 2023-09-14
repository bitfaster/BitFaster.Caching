
using System.Collections.Concurrent;
using System.Collections.Generic;
using BitFaster.Caching.Atomic;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Atomic
{
    public class ConcurrentDictionaryExtensionTests
    {
        private ConcurrentDictionary<int, AtomicFactory<int, int>> dictionary = new ConcurrentDictionary<int, AtomicFactory<int, int>>();

        [Fact]
        public void WhenItemIsAddedItCanBeRetrieved()
        {
            dictionary.GetOrAdd(1, k => k);

            dictionary.TryGetValue(1, out int value).Should().BeTrue();
            value.Should().Be(1);
        }

        [Fact]
        public void WhenItemIsAddedWithArgItCanBeRetrieved()
        {
            dictionary.GetOrAdd(1, (k,a) => k + a, 2);

            dictionary.TryGetValue(1, out int value).Should().BeTrue();
            value.Should().Be(3);
        }

        [Fact]
        public void WhenItemIsAddedItCanBeRemovedByKey()
        {
            dictionary.GetOrAdd(1, k => k);

            dictionary.TryRemove(1, out int value).Should().BeTrue();
            value.Should().Be(1);
        }

        [Fact]
        public void WhenItemIsAddedItCanBeRemovedByKvp()
        {
            dictionary.GetOrAdd(1, k => k);

            dictionary.TryRemove(new KeyValuePair<int, int>(1, 1)).Should().BeTrue();
            dictionary.TryGetValue(1, out _).Should().BeFalse();
        }
    }
}
