using System;
using System.Linq;
using BitFaster.Caching.Lfu;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class CacheExtTests
    {
        [Fact]
        public void CanUseICache2FromClassicLru()
        {
            var cache = new ClassicLru<int, string>(5);
            var cache2 = (ICacheExt<int, string>)cache;
            cache2.GetOrAdd(42, static (k, i) => (k + i).ToString(), 1).Should().Be("43");
            cache2.TryRemove(43, out _).Should().BeFalse();
            var first = cache2.First();
            cache2.TryRemove(first).Should().BeTrue();
        }

        [Fact]
        public void CanUseICache2FromConcurrentLfuBuilder()
        {
            var cache = new ConcurrentLfuBuilder<int, string>()
                .WithCapacity(5)
                .Build();

            var cache2 = (ICacheExt<int, string>)cache;
            cache2.GetOrAdd(42, static (k, i) => (k + i).ToString(), 1).Should().Be("43");
            cache2.TryRemove(43, out _).Should().BeFalse();
            var first = cache2.First();
            cache2.TryRemove(first).Should().BeTrue();
        }

        [Fact]
        public void CanUseICache2FromConcurrentLruBuilder()
        {
            var cache = new ConcurrentLruBuilder<int, string>()
                .WithCapacity(5)
                .WithExpireAfterAccess(TimeSpan.FromSeconds(5))
                .Build();

            var cache2 = (ICacheExt<int, string>)cache;
            cache2.GetOrAdd(42, static (k, i) => (k + i).ToString(), 1).Should().Be("43");
            cache2.TryRemove(43, out _).Should().BeFalse();
            var first = cache2.First();
            cache2.TryRemove(first).Should().BeTrue();
        }
    }
}
