
using Moq;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
// backcompat: remove 
#if NETCOREAPP3_1_OR_GREATER
    public class CacheEventsTests
    {
        [Fact]
        public void WhenInterfaceDefaultItemUpdatedRegisteredNoOp()
        {
            var metrics = new Mock<ICacheEvents<int, int>>();
            metrics.CallBase = true;

            metrics.Object.ItemUpdated += NoOpItemUpdated;
            metrics.Object.ItemUpdated -= NoOpItemUpdated;
        }

        private void NoOpItemUpdated(object sender, ItemUpdatedEventArgs<int, int> e)
        {
        }
    }
#endif
}
