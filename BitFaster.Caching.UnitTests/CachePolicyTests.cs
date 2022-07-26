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

            var cp = new CachePolicy(eviction.Object, expire.Object);

            cp.Eviction.Should().Be(eviction.Object);
            cp.ExpireAfterWrite.Should().Be(expire.Object);
        }
    }
}
