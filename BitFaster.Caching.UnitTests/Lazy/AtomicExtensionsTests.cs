using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lazy
{
    public class AtomicExtensionsTests
    {
        private ConcurrentLru<int, Atomic<int, int>> lru = new(2, 9, EqualityComparer<int>.Default);

        [Fact]
        public void GetOrAdd()
        {
            var rr = lru.GetOrAdd(1, i => i);
        }

        [Fact]
        public void TryUpdateWhenKeyDoesNotExistReturnsFalse()
        {
            lru.TryUpdate(2, 3).Should().BeFalse();
        }

        [Fact]
        public void TryUpdateWhenKeyExistsUpdatesValue()
        {
            lru.AddOrUpdate(1, 2);

            lru.TryUpdate(1, 42).Should().BeTrue();

            lru.TryGet(1, out int v).Should().BeTrue();
            v.Should().Be(42);
        }

        [Fact]
        public void TryGetWhenKeyDoesNotExistReturnsFalse()
        {
            lru.TryGet(1, out int v).Should().BeFalse();
        }

        [Fact]
        public void AddOrUpdateUpdatesValue()
        {
            lru.AddOrUpdate(1, 2);

            lru.TryGet(1, out int v).Should().BeTrue();
            v.Should().Be(2);
        }
    }
}
