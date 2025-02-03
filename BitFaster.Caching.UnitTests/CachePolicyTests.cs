using Shouldly;
using Moq;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class CachePolicyTests
    {
        [Fact]
        public void WhenCtorFieldsAreAssigned()
        {
            var eviction = new Mock<IBoundedPolicy>();
            var expire = new Mock<ITimePolicy>();

            var cp = new CachePolicy(new Optional<IBoundedPolicy>(eviction.Object), new Optional<ITimePolicy>(expire.Object));

            cp.Eviction.Value.ShouldBe(eviction.Object);
            cp.ExpireAfterWrite.Value.ShouldBe(expire.Object);
            cp.ExpireAfterAccess.HasValue.ShouldBeFalse();
            cp.ExpireAfter.HasValue.ShouldBeFalse();
        }
    }
}
