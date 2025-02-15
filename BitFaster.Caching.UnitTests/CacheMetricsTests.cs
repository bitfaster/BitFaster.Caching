using Shouldly;
using Moq;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
// backcompat: remove 
#if NETCOREAPP3_1_OR_GREATER
    public class CacheMetricsTests
    {
        [Fact]
        public void WhenInterfaceDefaultUpdatedInvokedReturnZero()
        { 
            var metrics = new Mock<ICacheMetrics>();
            metrics.CallBase = true;

            metrics.Object.Updated.ShouldBe(0);
        }
    }
#endif
}
