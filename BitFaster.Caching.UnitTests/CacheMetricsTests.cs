
using FluentAssertions;
using Moq;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    // backcompat: remove 
#if NET
    public class CacheMetricsTests
    {
        [Fact]
        public void WhenInterfaceDefaultUpdatedInvokedReturnZero()
        { 
            var metrics = new Mock<ICacheMetrics>();
            metrics.CallBase = true;

            metrics.Object.Updated.Should().Be(0);
        }
    }
#endif
}
