using System;
using BitFaster.Caching.Lru.Builder;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class LruInfoTests
    {
        [Fact]
        public void WhenExpiryNullGetExpiryReturnsNull()
        {
            var info = new LruInfo<int>();

            info.GetExpiry<string>().Should().BeNull();
        }

        [Fact]
        public void WhenExpiryCalcValueTypeDoesNotMatchThrows()
        {
            var info = new LruInfo<int>();

            info.SetExpiry<int>(new TestExpiryCalculator<int, int>());

            Action act = () => info.GetExpiry<string>();
            act.Should().Throw<InvalidOperationException>();
        }
    }
}
