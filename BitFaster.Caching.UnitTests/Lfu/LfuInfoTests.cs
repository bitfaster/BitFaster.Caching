using System;
using BitFaster.Caching.Lfu.Builder;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class LfuInfoTests
    {
        [Fact]
        public void WhenExpiryNullGetExpiryReturnsNull()
        {
            var info = new LfuInfo<int>();

            info.GetExpiry<string>().Should().BeNull();
        }

        [Fact]
        public void WhenExpiryCalcValueTypeDoesNotMatchThrows()
        {
            var info = new LfuInfo<int>();

            info.SetExpiry<int>(new TestExpiryCalculator<int, int>());

            Action act = () => info.GetExpiry<string>();
            act.Should().Throw<InvalidOperationException>();
        }
    }
}
