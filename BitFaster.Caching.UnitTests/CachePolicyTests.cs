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

        [Fact]
        public void TryTrimWhenTrimNotSupportedReturnsFalse()
        {
            var cp = new CachePolicy(Optional<IBoundedPolicy>.None(), Optional<ITimePolicy>.None());

            cp.TryTrimExpired().Should().BeFalse();
        }

        [Fact]
        public void TryTrimWhenExpireAfterWriteReturnsTrue()
        {
            var expire = new Mock<ITimePolicy>();
            var cp = new CachePolicy(Optional<IBoundedPolicy>.None(), new Optional<ITimePolicy>(expire.Object));

            cp.TryTrimExpired().Should().BeTrue();
        }

        [Fact]
        public void TryTrimWhenExpireAfterAccessReturnsTrue()
        {
            var expire = new Mock<ITimePolicy>();
            var cp = new CachePolicy(Optional<IBoundedPolicy>.None(), Optional<ITimePolicy>.None(), new Optional<ITimePolicy>(expire.Object), Optional<IDiscreteTimePolicy>.None());

            cp.TryTrimExpired().Should().BeTrue();
        }

        [Fact]
        public void TryTrimWhenExpireAfterReturnsTrue()
        {
            var expire = new Mock<IDiscreteTimePolicy>();
            var cp = new CachePolicy(Optional<IBoundedPolicy>.None(), Optional<ITimePolicy>.None(), Optional<ITimePolicy>.None(), new Optional<IDiscreteTimePolicy>(expire.Object));

            cp.TryTrimExpired().Should().BeTrue();
        }
    }
}
