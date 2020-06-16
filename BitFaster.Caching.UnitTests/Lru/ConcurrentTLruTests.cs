using FluentAssertions;
using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ConcurrentTLruTests
    {
        private readonly TimeSpan timeToLive = TimeSpan.FromSeconds(1);
        private const int capacity = 9;
        private ConcurrentTLru<int, string> lru;

        private ValueFactory valueFactory = new ValueFactory();

        public ConcurrentTLruTests()
        {
            lru = new ConcurrentTLru<int, string>(1, capacity, EqualityComparer<int>.Default, timeToLive);
        }

        [Fact]
        public void ConstructAddAndRetrieveWithDefaultCtorReturnsValue()
        {
            var x = new ConcurrentTLru<int, int>(3);

            x.GetOrAdd(1, k => k).Should().Be(1);
        }

        [Fact]
        public async Task WhenItemIsExpiredItIsRemoved()
        {
            lru.GetOrAdd(1, valueFactory.Create);

            await Task.Delay(timeToLive * 2);

            lru.TryGet(1, out var value).Should().BeFalse();
        }

        [Fact]
        public void WhenItemIsAddedThenRetrievedHitRatioIsHalf()
        {
            lru.GetOrAdd(1, valueFactory.Create);
            bool result = lru.TryGet(1, out var value);

            lru.HitRatio.Should().Be(0.5);
        }
    }
}
