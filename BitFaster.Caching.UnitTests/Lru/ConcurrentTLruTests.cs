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
        private readonly TimeSpan timeToLive = TimeSpan.FromMilliseconds(10);
        private const int capacity = 9;
        private ConcurrentTLru<int, string> lru;

        private ValueFactory valueFactory = new ValueFactory();

        public ConcurrentTLruTests()
        {
            lru = new ConcurrentTLru<int, string>(1, capacity, EqualityComparer<int>.Default, timeToLive);
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
