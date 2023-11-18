using FluentAssertions;
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

            cp.Eviction.Value.Should().Be(eviction.Object);
            cp.ExpireAfterWrite.Value.Should().Be(expire.Object);
            cp.ExpireAfterAccess.HasValue.Should().BeFalse();
            cp.ExpireAfter.HasValue.Should().BeFalse();
        }
    }
}
