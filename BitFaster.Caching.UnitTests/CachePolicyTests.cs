using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            var cp = new CachePolicy(eviction.Object, Optional<ITimePolicy>.From(expire.Object));

            cp.Eviction.Value.Should().Be(eviction.Object);
            cp.ExpireAfterWrite.Value.Should().Be(expire.Object);
        }
    }
}
