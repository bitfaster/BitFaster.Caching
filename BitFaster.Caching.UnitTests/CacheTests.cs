
using System;
using FluentAssertions;
using Moq;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class CacheTests
    {
// backcompat: remove conditional compile
#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public void WhenInterfaceDefaultGetOrAddFallback()
        {
            var cache = new Mock<ICache<int, int>>();
            cache.CallBase = true;

            Func<int, Func<int, int>, int> evaluate = (k, f) => f(k);
            cache.Setup(c => c.GetOrAdd(It.IsAny<int>(), It.IsAny<Func<int, int>>())).Returns(evaluate);

            cache.Object.GetOrAdd(
                1, 
                (k, a) => k + a, 
                2).Should().Be(3);
        }
#endif
    }
}
