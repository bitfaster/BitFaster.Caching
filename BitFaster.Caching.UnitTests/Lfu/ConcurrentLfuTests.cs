using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class ConcurrentLfuTests
    {
        [Fact]
        public async Task Scenario()
        {
            var cache = new ConcurrentLfu<int, int>(20);

            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(1, k => k);
            cache.GetOrAdd(2, k => k);
            cache.GetOrAdd(2, k => k);

            for (int i = 0; i < 25; i++)
            {
                cache.GetOrAdd(i, k => k);
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            cache.TryGet(1, out var value1).Should().BeTrue();
            cache.TryGet(2, out var value2).Should().BeTrue();
            cache.Count.Should().Be(20);
        }

        [Fact]
        public void WhenItemIsUpdatedItIsUpdated()
        {
            var cache = new ConcurrentLfu<int, int>(20);

            cache.GetOrAdd(1, k => k);
            cache.AddOrUpdate(1, 2);

            cache.TryGet(1, out var value).Should().BeTrue();
            value.Should().Be(2);
        }

        [Fact]
        public void WhenItemIsRemovedItIsRemoved()
        {
            var cache = new ConcurrentLfu<int, int>(20);

            cache.GetOrAdd(1, k => k);

            cache.TryRemove(1).Should().BeTrue();
            cache.TryGet(1, out var value).Should().BeFalse();
        }

        [Fact]
        public void BenchSim()
        {
            var cache = new ConcurrentLfu<int, int>(9);
            Func<int, int> func = x => x;

            for (int i = 0; i < 1000000; i++)
            {
                cache.GetOrAdd(1, func);
            }

            cache.GetOrAdd(1, func);
        }
    }
}
