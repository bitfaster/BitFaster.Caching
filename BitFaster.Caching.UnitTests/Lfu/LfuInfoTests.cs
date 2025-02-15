using System;
using BitFaster.Caching.Lfu.Builder;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class LfuInfoTests
    {
        [Fact]
        public void WhenExpiryNullGetExpiryReturnsNull()
        {
            var info = new LfuInfo<int>();

            info.GetExpiry<string>().ShouldBeNull();
        }

        [Fact]
        public void WhenExpiryCalcValueTypeDoesNotMatchThrows()
        {
            var info = new LfuInfo<int>();

            info.SetExpiry<int>(new TestExpiryCalculator<int, int>());

            Action act = () => info.GetExpiry<string>();
            act.ShouldThrow<InvalidOperationException>();
        }
    }
}
