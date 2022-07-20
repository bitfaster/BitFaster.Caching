using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class CapacityPartitionExtensionsTests
    {
        [Fact]
        public void WhenCapacityIsValidDoesNotThrow()
        {
            var p = new TestCapacityPartition { Cold = 2, Warm = 2, Hot = 2 };

            Action validate = () => { p.Validate(); };

            validate.Should().NotThrow();
        }

        [Fact]
        public void WhenColdIsZeroThrows()
        {
            var p = new TestCapacityPartition { Cold = 0, Warm = 2, Hot = 2 };

            Action validate = () => { p.Validate(); };

            validate.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenWarmIsZeroThrows()
        {
            var p = new TestCapacityPartition { Cold = 2, Warm = 0, Hot = 2 };

            Action validate = () => { p.Validate(); };

            validate.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WhenHotIsZeroThrows()
        {
            var p = new TestCapacityPartition { Cold = 2, Warm = 2, Hot = 0 };

            Action validate = () => { p.Validate(); };

            validate.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
